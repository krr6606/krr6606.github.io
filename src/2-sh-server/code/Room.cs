using System.Buffers;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;


namespace HelloServer;
public class GameConfig
{
    public float stateGameStartTime { get; set; }
    public float stateGenreAssignAndLiarSelectTime{ get; set; }
    public float stateKeywordDistributeTime{ get; set; }
    public float stateMartEnterTime{ get; set; }
    public float stateMartMoveTime{ get; set; }

    public float stateMartReturnTime{ get; set; }
    public float stateShowItemAndSpeakTime{ get; set; }
    public float stateSpeechEndTime{ get; set; }

    public float statePointAtSuspectTime{ get; set; }
    public float statePointAtSuspectEndTime{ get; set; }

    public float stateLiarOutButtonPressedTime{ get; set; }

    public float stateDebateTime{ get; set; }
    public float stateDebateEndTime{ get; set; }

    public float stateVoteTime{ get; set; }
    public float stateVoteEndTime{ get; set; }

    public float stateLiarConfirmedTime{ get; set; }
    public float stateLiarKeywordGuessTime{ get; set; }
    public float stateLiarKeywordGuessEndTime{ get; set; }

    public float stateScoreTallyTime{ get; set; }
    public float stateScoreTallyEndTime{ get; set; }
  
    public float stateFinalResultTime{ get; set; }
    public float stateFinalResultEndTime{ get; set; }

    public int MaxRound{ get; set; }
    public int MaxCycle{ get; set; }
    public int MaxCategoryItemCount{ get; set; }
    
    public int VoteScoreChangeAmount { get; set; }
    public int KeywordGuessScoreChangeAmount{ get; set; }
    public int LiarButtonScoreChangeAmount { get; set; }
    public int QuestScoreChangeAmount { get; set; }
    
}
// 방 하나. 방에 있는 사람들을 들고 있다가
// 메세지를 전달해 준다.
// 방의 기능은 아래와 같습니다
// 1. 들어온다 : JoinAsync
// 2. 말한다   : ReceiveLoopAsync => 결국 요게 채팅임
// 3. 나간다   : LeaveAsync
// 4. 뿌린다   : BroadcastStateAsync

// Room은 메시지를 무리는 법만 알고
// 언제 뿌릴지는 결정하지 않습니다. 

public class Room
{

    public  GameManager gameManager;

    private int isBroadcasting;
    private bool IsBroadcasting => Volatile.Read(ref isBroadcasting) == 1;
    
    private const int MaxMessageBytes = 1024 * 1024; // 들어오는 메세지 용량 1MB 제한
    // 접속자 한 명.
    public class Member
    {
        public Protocol.User User;
        public WebSocket Socket;

        // 원래는 벡터로 Position으로 묶어서 사용하는게 좋습니다.
        // 님들이 개발할때는 그렇게 하세여
        public float X;
        public float Y;
        public float Z;

        public int MovesSinceLog;

        public bool IsReady = false;

        public bool IsHost = false;

        public Protocol.PlayerState playerState = new Protocol.PlayerState();
        // DateTime?
        // : 날짜랑 시간을 표현하고 조작할 때 사용하는 구조체 입니다.
        //  DateTime.Now : 현재 지역 시간을 나타낼 수 있ㅅ브니다
        //  DateTime.UtcNow : 협정 세계시(영국 본초 자오선(?))
        // 출력 서식을 따로 지정할 수 있습니다. 그거는 MS 홈페이지 가서 보세요
        public DateTime LastLogAt;
        
        // Member 안에 SendLock 대신:
        public readonly Channel<byte[]> Outbox = 
            Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });
        
        public readonly CancellationTokenSource WriterCts = new CancellationTokenSource();
        public Task WriterTask; // 나중에 정리(await)하려고 참조를 들고 있는다
    }
    
    // race condition이 일어나도 여러 쓰레드에서 동시적으로
    // 참조 하여 읽을 수 있는 딕셔너리 입니다. 일반적인 Dictionary를 쓰면
    // race condition이 발생하면 깨질꺼에여.
    // 여러 쓰레드에서 동시에 사용하더라도 딕셔너리의 한 상태를 유지 시킬 수 있는
    // 안정성이 보장된 딕셔너리 입니다.
    public readonly ConcurrentDictionary<string, Member> members = new();

    // 들어오고 나가는 메시지 처리(일)을 한줄로 세우는 자물쇠 입니다.
    // lock블록이 await가 안먹어서 사용합니다.
    private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
    public readonly string code; // 방번호



    
    public bool IsEmpty => members.IsEmpty;
    public GameConfig GameConfig { get; }
    public int roomExpired;
    public bool RoomExpired => Volatile.Read(ref roomExpired) == 1;
    private readonly CancellationTokenSource roomExpiredCancellation = new();
    public Room(string code, GameConfig config)
    {
        this.code = code;

        this.GameConfig = config;
        gameManager = new GameManager(config,this);
    }
    
    
    private static async Task WriterLoopAsync(Member member)
    {
        try
        {
            // 채널 자체 취소 + 개별 Send 타임아웃을 하나로 묶는다
            await foreach (byte[] bytes in member.Outbox.Reader.ReadAllAsync(member.WriterCts.Token))
            {
                if (member.Socket.State != WebSocketState.Open) continue;

                using var sendCts = CancellationTokenSource.CreateLinkedTokenSource(member.WriterCts.Token);
                sendCts.CancelAfter(TimeSpan.FromSeconds(1));

                try
                {
                    await member.Socket.SendAsync(bytes, WebSocketMessageType.Text, true, sendCts.Token);
                }
                catch (OperationCanceledException) when (member.WriterCts.IsCancellationRequested == false)
                {
                    // 멤버가 나가려는 게 아니라 진짜 타임아웃 → 끊긴 걸로 간주
                    member.Socket.Abort();
                }
                catch (WebSocketException) { }
            }
        }
        catch (OperationCanceledException)
        {
            // WriterCts가 취소됨 = 정상적인 퇴장/종료 신호. 조용히 빠져나온다.
        }
    }

    // 게임의 틱 업데이트(진행)을 담당하는 함수( RoomHub의 모든 룸의 함수를 실행시키는 곳에서 실행된다.)
    public void GameTick()
    {
        gameManager.Tick();
    }
    
    #region 듣기

    // 글자를 받는다. 상대가 연결을 닫았으면 null을 돌려준다
    // PS
    // : 긴글자일 경우 가끔 조각으로 나뉘어서 오는 경우가 있다.
    //  우리가 StreamReader를 다뤘을때 처럼 메시지의 끝 EndOf~~
    //  을 정확하게 파악하여 메시시 한 단위를 만들어 준다.
    private static async Task<string> ReceiveTextAsync
        (WebSocket socket, CancellationToken token)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);
        StringBuilder builder = new StringBuilder();
        Decoder decoder = Encoding.UTF8.GetDecoder();
        int totalBytes = 0;

        try
        {
            while (true)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                }
                catch (WebSocketException)
                {
                    return null;
                }

                if (result.MessageType == WebSocketMessageType.Close) return null;

                int charCount = decoder.GetCharCount(buffer, 0, result.Count, false);
                char[] chars = ArrayPool<char>.Shared.Rent(charCount);
                try
                {
                    decoder.GetChars(buffer, 0, result.Count, chars, 0, false);
                    builder.Append(chars, 0, charCount);
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(chars);
                }

                totalBytes += result.Count;
                if (totalBytes > MaxMessageBytes)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "들어온 메세지가 상한을 넘었습니다.", token);
                    return null;
                }
                
                if (result.EndOfMessage) return builder.ToString();
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
    // 멤버와 연결이 끊길때까지 멤버가 보낸 메시지를 계속 듣는다. 
    private async Task ReceiveLoopAsync(Member member, CancellationToken token)
    {
        // 토큰에 취소 요청이 없으면 계속 돈다
        while (token.IsCancellationRequested == false)
        {
            if (RoomExpired)
            {
               await SendAsync(member, new Protocol.LeaveMessage{Id = member.User.Id});
               return;
            }
            string text = await ReceiveTextAsync(member.Socket, token);
            // text가 비어있으면 닫았다는 뜻
            if (string.IsNullOrEmpty(text)) return;

            // 아래부터는 Json 텍스트 처리가 된다.
            // 분기에 따라 알맞은 처리 함수를 선택하여 실행해 준다.
            // JsonSerializer?
            // C#에서 Json의 직렬화, 역직렬화를 담당하는 클래스 입니다.
            // Unity와 C#에서 사용하는 직렬화 클래스가 다른것에 유의하세여
            // (타입이랑 매개변수로 텍스트만 넘기면 알아서 잘 처리해줍니다)
            Protocol.TypeOnly kind;
            try { kind = JsonSerializer.Deserialize<Protocol.TypeOnly>(text); }
            catch (JsonException)
            {
                Console.WriteLine($"[{code}] {member.User.Id} 가 해석 불가능한 메시지를 보냄. 버림 처리.");
                continue;
            }
            if (kind?.Type != "move")
            {
                Console.WriteLine($"[Type] 들어온 타입 : {kind?.Type}");
                
            }
            if(kind?.Type == "move") HandleMove(member, text);
            else if(kind?.Type == "chat") await HandleChatAsync(member, text);
            else if (kind?.Type == "ready") await HandleReady(member, text);
            else if (kind?.Type == "게임 시작") await HandleGameStart(member);
            else if(kind?.Type == "NonPoint")  await HandleNonPoint(member, text);
            else if(kind?.Type == "Select") await HandleSelectUser( member, text);
            else if(kind?.Type == "LiarSelfDisclose") HandleLiarButtonPressed(member, text);
            else if (kind?.Type == "Vote") await HandleVote(member, text);
            else if(kind?.Type == "userInteraction") await HandleInteraction(member, text);
            else if(kind?.Type == "PushAnimation") await HandlePushAnimation(member, text);
            
            // 모르는 정보는 그냥 흘려버립니다.
            // Tip
            // : 여기 부분에 여러분이 넣고싶은 커스텀한 함수를 처리하는
            // 구간을 만들면 되겠죠?   
        }
    }

    #region 게임 정보 Handle 함수들

    private async Task HandlePushAnimation(Member member, string text)
    {
        if(gameManager.currentTurnState != gameManager.martMoveState) return;
        Protocol.PushAnimationMessage pushAnimationMessage = JsonSerializer.Deserialize<Protocol.PushAnimationMessage>(text);
        if(pushAnimationMessage == null) return;
        pushAnimationMessage.UserID = member.User.Id;
       await BroadcastAsync(pushAnimationMessage);
    }

    private async Task HandleInteraction(Member member, string text)
    {
        if(gameManager.currentTurnState != gameManager.martMoveState) return;
        
        Protocol.UserInteractionMessage interactionMessage = JsonSerializer.Deserialize<Protocol.UserInteractionMessage>(text);

        if (string.IsNullOrEmpty(interactionMessage?.senderId) || string.IsNullOrEmpty(interactionMessage?.receivedId))
        {
            Console.WriteLine($"[null이거나 빈 메세지]");
            return;
        }
        interactionMessage.senderId = member.User.Id;
        switch (interactionMessage.InteractionType)
        {
            case Protocol.InteractionType.PushQuery:
            {
                interactionMessage.InteractionType = Protocol.InteractionType.PushAnswer;
                interactionMessage.IsSuccess = true;
                break;
            }
            case Protocol.InteractionType.ItemHoldQuery:
            {
                bool won = gameManager.itemOwnersDic.TryAdd(interactionMessage.receivedId, member.User.Id);
                if (won == false)
                {
                    Console.WriteLine($"[레이스 컨디션으로 인한 리턴] 건드린 id {member.User.Id}, 건드려진 id{interactionMessage.receivedId}");
                    return;
                }
                interactionMessage.InteractionType = Protocol.InteractionType.ItemHoldAnswer;
                interactionMessage.IsSuccess = true;
                
                break;
            }
            case Protocol.InteractionType.ItemDropQuery:
            {

                if (gameManager.itemOwnersDic.TryRemove(interactionMessage.receivedId, out string userId))
                {
                    Console.WriteLine($"[내려놓기 성공]: {userId}");
                }
                else
                {
                    Console.WriteLine("[내려놓기 실패]:(키가 없음)");
                }
                interactionMessage.InteractionType = Protocol.InteractionType.ItemDropAnswer;
                interactionMessage.IsSuccess = true;
                break;
            }
            case Protocol.InteractionType.ItemPutInBagQuery:
            {
                if (gameManager.itemOwnersDic.ContainsKey(interactionMessage.receivedId) ==false)
                {
                    Console.WriteLine($"[홀드된 아이템에 없는 경우 리턴] 건드린 id {interactionMessage.senderId}, 건드려진 id{interactionMessage.receivedId}");
                    return;
                }

                interactionMessage.IsSuccess = true;
                interactionMessage.InteractionType = Protocol.InteractionType.ItemPutInBagAnswer;


                if (int.TryParse(interactionMessage.receivedId, out int holdItemId) == false)
                {
                    interactionMessage.IsSuccess = false;
                    Console.WriteLine($"[아이템 체인지 로직] 해석되지 않는 아이디 들어옴");
                    break;
                }
                ItemDef holdItemDef = DataManager.Instance.GetItemDef(holdItemId);
                if (holdItemDef == null)
                {
                    interactionMessage.IsSuccess = false; // 잘못된 아이템 ID
                    Console.WriteLine($"[아이템 체인지 로직] : 아이템이 존재하지 않는 ID {interactionMessage.receivedId}");

                }
                bool IsNeedChanged = false;
                int nullCount = 0;
                for(int j = 0; j < gameManager.currentRoom.GameConfig.MaxCycle; j++)
                {
                    if (gameManager.UserGameInfos[member.User.Id].ItemIds[j] == null)
                    {
                        nullCount++;
                        continue;
                    }
                    int.TryParse(gameManager.UserGameInfos[member.User.Id].ItemIds[j],  out int bagInItemId);
                    ItemDef currentItemDef = DataManager.Instance.GetItemDef(bagInItemId) ?? new ItemDef();
                    if (string.IsNullOrEmpty(currentItemDef?.ItemId.ToString()))
                    {
                        Console.WriteLine($"[가방 속 아이템 해석] 가방에 아이템이 있지만 유효한 값이 아님");
                    }

                    if (currentItemDef.CategoryType == holdItemDef.CategoryType)
                    {
                        IsNeedChanged = true;
                    }
                }

                if (IsNeedChanged)
                {
                    if (gameManager.UserGameInfos.TryGetValue(member.User.Id, out var userInfo))
                    {
                        bool found = false;
                        for (int i = 0; i < userInfo.ItemIds.Length; i++)
                        {

                            string selectedItemId = userInfo.ItemIds[i] ?? "";
                            if (string.IsNullOrEmpty(selectedItemId)) continue;
                            if (!int.TryParse(selectedItemId, out int id)) continue;

                            ItemDef currentItem = DataManager.Instance.GetItemDef(id);
                            if (currentItem != null && holdItemDef.CategoryType == currentItem.CategoryType)
                            {

                                interactionMessage.Parameter = JsonSerializer.Serialize(
                                    new Protocol.ItemPutInBagParameter
                                        { ChangedItemId = currentItem.ItemId.ToString() });


                                gameManager.itemOwnersDic.TryRemove(currentItem.ItemId.ToString(), out _);


                                userInfo.ItemIds[i] = holdItemDef.ItemId.ToString();


                                found = true;
                                break;
                            }
                        }


                        Console.WriteLine($"[아이템 체인지 로직] : 바꾸기 성공 여부 : {found}");
                        interactionMessage.IsSuccess = found; // 못 찾았으면 실패로
                    }
                    else
                    {
                        interactionMessage.IsSuccess = false; // 유저 정보 없음
                        Console.WriteLine($"[아이템 체인지 로직] : 유저 정보 없음");
                    }

                }
                else if (nullCount != 0)
                {
                    bool isSuccess = false;
                    for (int i = 0; i < gameManager.currentRoom.GameConfig.MaxCycle; i++)
                    {
                        if (gameManager.UserGameInfos[member.User.Id].ItemIds[i] == null && gameManager.AllCategories[i] == holdItemDef?.CategoryType)
                        {
                            gameManager.UserGameInfos[member.User.Id].ItemIds[i] = interactionMessage.receivedId;
                            isSuccess = true;
                            break;
                        }
                    }
                    Console.WriteLine($"[일반적인 아이템 추가] : 성공 여부 : {isSuccess}");
                }
                else
                {
                    Console.WriteLine($"[비어있지도 않지만, 같은 카테고리도 발견하지 못한 버그]");
                }
                break;
                
            }
        }

       await BroadcastAsync(interactionMessage);
    }
    private async Task HandleVote(Member member, string text)
    {
        if(gameManager.currentTurnState != gameManager.voteState) return;
        
// Room.HandleVote — 나중에 누른 것으로 덮어쓴다(마음 바꾸기 허용). 중복이 쌓이지 않는다.
        Protocol.VoteMessage voteMessage = JsonSerializer.Deserialize<Protocol.VoteMessage>(text);
        if (voteMessage == null) return;
        voteMessage.UserID = member.User.Id;
        gameManager.Votes[member.User.Id] = voteMessage;
       await BroadcastAsync(voteMessage);
    }

    private void HandleLiarButtonPressed(Member member, string text)
    {

        if(gameManager.currentTurnState != gameManager.showItemAndSpeakState) return;
        
        if (member.playerState.IsLiar)
        {
            gameManager.PressedLiarId = member.User.Id;
        }
        else
        {
            gameManager.LiarOutButtonQueue.Enqueue(member.User.Id);
        }
    }

    private async Task HandleNonPoint(Member member,string text)
    {
        if(gameManager.currentTurnState != gameManager.pointAtSuspectState) return;
        
        if (gameManager.SkipUsers.TryAdd(member.User.Id, true) == false) return;
        Protocol.NonPointMessage nonPointMessage = new Protocol.NonPointMessage();
        nonPointMessage.UserID = member.User.Id;
        await BroadcastAsync(nonPointMessage);
    }

    private async Task HandleSelectUser(Member member, string text)
    {
        if(gameManager.currentTurnState != gameManager.pointAtSuspectState) return;
        
        Protocol.SelectMessage selectMessage = JsonSerializer.Deserialize<Protocol.SelectMessage>(text);

        Console.WriteLine($"[실제 지목 메세지] 지목 당한 유저 {selectMessage.selectedID}");
        gameManager.PointInfo[member.User.Id] = selectMessage.IsSelectCancel ? "" : selectMessage.selectedID;
        foreach (var pointInfoDic in gameManager.PointInfo)
        {
            Console.WriteLine($"[지목 핸들] 지목 딕셔너리 {pointInfoDic.Key} : {pointInfoDic.Value}");
        }
        
        await BroadcastAsync(selectMessage);
    }
    private async Task HandleReady(Member member, string text)
    {
       Protocol.ReadyMessage readyMessage = JsonSerializer.Deserialize<Protocol.ReadyMessage>(text);

       members[member.User.Id].IsReady = true;
       Console.WriteLine($"[{code}] {readyMessage.ID} : 준비 버튼을 눌렀다!");
       bool isAllReady = false;
       int count = 0;
       foreach (Member m in members.Values)
       {
           if(m.IsHost) continue;
           if (m.IsReady)
           {
               count++;

           }
       }
       readyMessage.readyCount = count;
       if(count >= members.Count - 1) isAllReady = true;
       
       await BroadcastAsync(readyMessage);
       if (isAllReady)
       {

           foreach (Member m in members.Values)
           {
               if (m.IsHost)
               {

                  await SendAsync(m, new Protocol.AllReadyMessage());

               }
           }

       }
       
    }

    private async Task HandleGameStart(Member member)
    {
        // 시작은 호스트만 누른다.
        if (member.IsHost == false) return;
        if(members.Count < 3) return;

        foreach (var m in members.Values)
        {
            m.IsReady = false;
        }
        
        Protocol.GameStartOKMessage gameStartOkMessage = new Protocol.GameStartOKMessage();
        Protocol.NewGameConfig newGameConfig = new Protocol.NewGameConfig();
        newGameConfig.MaxCycle = GameConfig.MaxCycle;
        newGameConfig.MaxRound = GameConfig.MaxRound;
        gameStartOkMessage.newGameConfig = newGameConfig;

        await BroadcastAsync(gameStartOkMessage);
        // 게임 루프 시작
        gameManager.GameStart();
        
    }
    

    // 이동 관련 메시지를 처리하는 함수
    private void HandleMove(Member member, string text)
    {
        if(gameManager.currentTurnState != gameManager.martMoveState) return;
        
        // 메시지를 읽어준다
        Protocol.MoveMessage move = JsonSerializer.Deserialize<Protocol.MoveMessage>(text);
        // move 메시지의 내용을 member의 X,Y 내용에 카피해준다
        member.X = move.X;
        member.Y = move.Y;
        member.Z = move.Z;

    }

    // 채팅 관련 메시지를 처리하는 함수
    private async Task HandleChatAsync(Member member, string text)
    {
        // 먼저 Chat메시지를 읽어 준다
        Protocol.ChatMessage chat = JsonSerializer.Deserialize<Protocol.ChatMessage>(text);
        // 온 메시지에서 사용자가 말한 부분만 읽어준다.
        // .Trim() 함수를 이용해서 앞,뒤 공백을 제거해준다
        string said = chat.Text?.Trim();
        Console.WriteLine($"[{chat.ChatType.ToString()}][{code}] {chat.NickName} : {said}");
        
        if (chat.ChatType == Protocol.ChatType.Normal)
        {
            await BroadcastAsync(chat);
            return;
        }
        bool isVaild = false;
        if (chat.ChatType == Protocol.ChatType.KeywordGuess )
        {
            if(gameManager.currentTurnState != gameManager.liarKeywordGuessState) return;

            gameManager.LiarGuessKeyWord = said;
            isVaild = true;
            
        }
        if (chat.ChatType == Protocol.ChatType.Special && gameManager.currentTurnState == gameManager.showItemAndSpeakState)
        {
            gameManager.ChangeSpeakerTrigger = true;
            
            isVaild = true;
            
        }

        if (isVaild)
        {
            await BroadcastAsync(chat);
        }
    }
    

    #endregion
    #endregion

    #region 뿌리기

    // 메시지를 여러명한테 뿌리는 함수
    public Task BroadcastAsync(object message, string exceptId = null)
    {
        
        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, message.GetType()));
        foreach (Member member in members.Values)
        {
            if (member.User.Id == exceptId) continue;
            member.Outbox.Writer.TryWrite(bytes); // 락 대기 없음, 순서 보장됨
        }
        return Task.CompletedTask;
    }
    // 한명의 User에게 메시지를 보내는 함수
    private Task SendRawAsync(Member member, string json)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        member.Outbox.Writer.TryWrite(bytes); // 락 대기 없음, 순서 보장됨
        return Task.CompletedTask;
    }


    // 단순 호출용 유틸 함수
    public Task SendAsync(Member member, object message)
    {
        return SendRawAsync(member, JsonSerializer.Serialize(message, message.GetType()));
    }

    // 지금 이 방의 사람들 위치를 한번씩 뿌린다.
    // 언제 뿌릴지는 RoomHub에서 정한다.
    public async Task BroadcastStateAsync()
    {
        // 방에 멤버가 없다면(방이 사라질때) 보내지 않는다.
        if (members.IsEmpty) return;
        if(IsBroadcasting) return;
        Interlocked.Exchange(ref isBroadcasting, 1);
        // 사람마다 위치 데이터 객체 하나씩 만든다.
        List<Protocol.PlayerState> states = new List<Protocol.PlayerState>();

        foreach (Member member in members.Values)
        {
            states.Add(new Protocol.PlayerState()
            {
                Id = member.User.Id,
                X = member.X,
                Y = member.Y,
                Z = member.Z,
            });
        }
        
        // states를 배열로 바꿔서 뿌린다(Broadcast)
        await BroadcastAsync(new Protocol.StateMessage() { States = states.ToArray() });
        Interlocked.Exchange(ref isBroadcasting, 0);
    }
    
    #endregion

    #region 들어오기, 나가기

    private async Task<Member> JoinAsync(WebSocket socket, 
        string id, CancellationToken token)
    {
        if (gameManager.IsGameRunning) return null;
        // 첫 메시지를 들어 봅니다. 지금 서버코드 규약에 따르면
        // hello 여야 합니다
        string first = await ReceiveTextAsync(socket, token);
        // 만약에 메시지가 이상하다면 종료
        if (string.IsNullOrEmpty(first)) return null;
        
        // 타입을 꺼내준다
        Protocol.TypeOnly kind = JsonSerializer.Deserialize<Protocol.TypeOnly>(first);
        // hello인지 확인해준다
        if (kind?.Type != "hello") 
        {
            Console.WriteLine($"[{code}] 첫 메시지가 hello가 아님 : {kind?.Type}  first : {first}");
            return null;
        }
        
        Protocol.HelloMessage hello = JsonSerializer.Deserialize<Protocol.HelloMessage>(first);
        if (string.IsNullOrWhiteSpace(hello?.NickName))
        {
            Console.WriteLine($"[{code}] hello 에 닉네임이 없다");
            return null;
        }
        
        // 아래서 부터는 정상처리
        // 메시지와 매개변수를 조합해서 Member객체를 생성한다.
        Member member = new Member();
        member.Socket = socket;
        member.LastLogAt = DateTime.Now; // 들어온 시각으로 맞춰 둔다.
        member.User = new Protocol.User();
        member.User.Id = id;
        member.User.NickName = hello.NickName.Trim();
        
        // 들어오고 나가는 일은 한사람에 한명씩 해야합니다.
        // 사람이 들어오면 현재 방에 있는 멤버들에게도 메시지를 보내줘야겠죠?
        // 그래서 여기서도 lock을 걸어줘야 하는데 await이기 때문에
        // lock을 못걸어서 gate를 이용해서 대기하여 처리합니다.
        await gate.WaitAsync(token);
        Protocol.WelcomeMessage welcome;

        try
        {
            // 누군가가 hello 메시지를 보냈으면
            // welcome 메시지를 이용해서
            // 현재 방 사람들을 접속한 유저에게 전송하고,
            // join 메시지를 다른 사람들에게 보내준다
            List<Protocol.User> already = new List<Protocol.User>();
            welcome = new Protocol.WelcomeMessage();
            foreach (Member other in members.Values)
                already.Add(other.User);

            // welcome 메시지를 전송
            welcome.RoomCode = code; // 서버 방정보를 보낸다
            welcome.User = member.User; // 서버에서 생성한 유저 정보를 접속자에게 보낸다
            welcome.Users = already.ToArray(); // 현재 방에 있는 유저들 정보를 보낸다
            int count = 0;
            foreach (Member m in members.Values)
            {
                if(m.IsHost) continue;
                if (m.IsReady)
                {
                    count++;

                }
            }

            welcome.ReadyCount = count;
          
            if (members.IsEmpty) member.IsHost = true; // 가장 처음 접속하면 호스트 취급한다.
            members[member.User.Id] = member;
       

        }
        finally
        {
            gate.Release();
        }
        await SendAsync(member, welcome);
        // join 메시지를 뿌린다. 접속자인 member 에게는 보내지 않는다
        await BroadcastAsync(new Protocol.JoinMessage { User = member.User }, member.User.Id);
        Console.WriteLine($"[{code}] {member.User.NickName}({member.User.Id}) 들어옴");
        return member;
    }

    private async Task LeaveAsync(Member member)
    {
        // 들어오기과 같은 자물쇠를 사용합니다.
        // 들어오기 나가기는 방의 멤버를 수정하고 메시지를 처리하기 때문에
        // 같은 자물쇠를 사용해줘야 합니다.
        // (안하면 유령객체 생길수도?)
        await gate.WaitAsync(TimeSpan.FromSeconds(1));

        try
        {
            if (member.IsHost && members.Count > 1)
            {
                foreach ( (string id,Member m) in members)
                {
                    if (m.IsHost == false)
                    {
                        m.IsHost = true;
                        break;
                    }
                }
            }
            members.TryRemove(member.User.Id, out _);
            if (gameManager.IsGameRunning && roomExpiredCancellation.IsCancellationRequested == false)
            {
                Interlocked.Exchange(ref roomExpired, 1);
                gameManager.GameEnd();
                
                await roomExpiredCancellation.CancelAsync();
            }
          
        }
        finally
        {
            gate.Release();
        }
        // 퇴장한것을 알려줍니다.
        await BroadcastAsync(new Protocol.LeaveMessage { Id = member.User.Id }, member.User.Id);
        Console.WriteLine($"[{code}] {member.User.NickName}({member.User.Id}) 나감");
    }

    // 한 사람의 접속부터 끊김까지 통째로 관리하느 ㄴ함수
    // id를 외부에서 전달받는 이유
    // : userID는 겹치면 안되기에 모든 유저를 관리하느 RoomHub에서 전달해 준다.
    public async Task HandleAsync(WebSocket socket,
        string id, CancellationToken token)
    {
        using CancellationTokenSource linkedCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                token, roomExpiredCancellation.Token);
        // Join처리를 실행하고 끝난뒤 멤버 객체를 저장해준다.
        Member member = await JoinAsync(socket, id, linkedCancellation.Token);
        // hello 안보내고 딴소리 했다. 방에 못 들인다
        if (member == null) return;
        member.WriterTask = Task.Run(() => WriterLoopAsync(member));
        try
        {
            // 접속완료 했으면 메시지를 계속 들을 수 있게
            // 루프를 호출해준다.
            await ReceiveLoopAsync(member, linkedCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            // 서버 꺼지는 중. 정상임
        }
        catch (WebSocketException)
        {
            
        }
        finally
        {
            // 1) writer에게 "그만 받아라" 신호
            await member.WriterCts.CancelAsync();
            member.Outbox.Writer.TryComplete();

            // 2) writer가 실제로 끝날 때까지 기다려서 리소스 정리 확인
            try { await member.WriterTask; } catch { /* 취소로 인한 예외는 무시 */ }
            member.WriterCts.Dispose();
            // 루프가 종료되었으면 연결이 끊어진 것
            // 퇴장 처리 해준다
            await LeaveAsync(member);
            if (socket.State == WebSocketState.Open)
            {
                // 클라이언트가 '정상 종료'로 인식하도록 닫기 인사를 보낸다.
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
            }
        }
    }
    
    #endregion
    
    

}