using System.Collections.Concurrent;
using HelloServer.State;
using Jay.FSM;

namespace HelloServer;

public class GameManager
{

    public class UserInfo
    {
        public Protocol.User user;
        public bool IsLiar;
        private string[] itemIds;

        public string[] ItemIds
        {
            get
            {
                lock (userLock)
                {
                    return  itemIds;
                }
  
            }
            set
            {
                lock (userLock)
                {
                    itemIds = value;
                }

            }
        }


        public int score;
        public bool IsQuestSuccess;
  
        public UserInfo(Protocol.User user, int score)
        {
            this.user = user;
            this.score = score;

            
        }

        public UserInfo()
        {
            
        }

        private readonly object userLock = new object();
    }
    StateMachine<IUpdatableState> stateMachine;

    public IState currentTurnState => stateMachine.CurrentState;

    // GameManager: bool 대신 int 를 쓴다
    private int isGameRunning;
    public bool IsGameRunning => Volatile.Read(ref isGameRunning) == 1;

    public Room currentRoom;

    
    public GameStartState startState;
    public GenreAssignAndLiarSelectState genreAssignAndLiarSelectState;
    public KeywordDistributeState keywordDistributeState;
    public MartEnterState martEnterState;
    public MartMoveState martMoveState;
    public MartReturnState martReturnState;
    public ShowItemAndSpeakState showItemAndSpeakState;
    public SpeechEndState speechEndState;
    public PointAtSuspectState pointAtSuspectState;
    public PointAtSuspectEndState pointAtSuspectEndState;
    public DebateTimeState debateTimeState;
    public DebateEndState debateEndState;
    public VoteState voteState;
    public VoteEndState voteEndState;
    public LiarConfirmedState liarConfirmedState;
    public LiarKeywordGuessState liarKeywordGuessState;
    public LiarKeywordGuessEndState liarKeywordGuessEndState;
    public ScoreTallyState scoreTallyState;
    public ScoreTallyEndState scoreTallyEndState;
    public FinalResultState finalResultState;
    public FinalResultEndState finalResultEndState;
    public LiarOutButtonPressedState  liarOutButtonPressedState;
    
    public CategoryType[] AllCategories;
    public CategoryType currentCategory;
    public int currentCycle = 0;
    public int currentRound = 0;
    public int currentSpeakedCount = 0;
    public int maxSpeakedCount = 0;
    // 라이어가 라이어 버튼을 눌렀을 경우 추가되는 필드
    private string liarButtonPressedUserId;
    
    public ConcurrentDictionary<string,UserInfo> UserGameInfos;
    public Protocol.User focausUser;


    // 1) lock 의 자물쇠는 lock 전용 객체로. await 없이 짧게 감싸는 용도이므로 object 가 맞다.
    private readonly object gameLock = new object();
    private string mostFrequent;
    public string MostFrequent
    {
        get
        {
            lock (gameLock)
            {
                return mostFrequent;
            }
        }
        set
        {
            lock (gameLock)
            {
                mostFrequent = value;
                
            }
        }
    }

    public GenreDef CurrentGanre = new GenreDef();
    public KeyWordDef CurrentKeyWord = new KeyWordDef();
    public KeyWordDef CurrentLiarKeyword = new KeyWordDef();
    public List<KeyWordDef> OldKeyWords = new List<KeyWordDef>();
    
    // 키값이 건드려진 대상의 ID, 벨류가 건드린 ID
    public readonly ConcurrentDictionary<string, string> itemOwnersDic = new();

    public ConcurrentDictionary<CategoryType,ConcurrentQueue<string>> AllMartItems = new ConcurrentDictionary<CategoryType,ConcurrentQueue<string>>();
   

    #region 비동기 함수에서 보내는 정보들
    // GameManager: ConcurrentQueue<VoteMessage> 대신
    public readonly ConcurrentDictionary<string, Protocol.VoteMessage> Votes = new();    
    // 라이어 버튼을 누른 '일반 유저ID'가 담기는 버튼
    public readonly ConcurrentQueue<string> LiarOutButtonQueue = new ConcurrentQueue<string>();


    private string liarId;

    public string LiarId
    {
        get
        {
            lock (gameLock)
            {
                return liarId;
            }
        }
        set
        {
            lock (gameLock)
            {
                liarId = value;
            }
        }
    }

    private string liarGuessKeyWord;

    public string LiarGuessKeyWord
    {

        get
        {
            lock (gameLock)
            {
                return liarGuessKeyWord;
            }
        }
        set
        {
            lock (gameLock)
            {
                liarGuessKeyWord = value;
            }
        }
    }


    private bool changeSpeakerTrigger = false;
    public bool ChangeSpeakerTrigger
    {
        get
        {
            lock (gameLock)
            {
                return changeSpeakerTrigger;
            }
        }
        set
        {
            lock (gameLock)
            {
                changeSpeakerTrigger = value;
            }
        }
    }

    public string PressedLiarId
    {
        get
        {
            lock (gameLock)
            {
                return liarButtonPressedUserId;
            }

        }
        set{
            lock (gameLock)
            {

                    liarButtonPressedUserId = value;
                
            }
        }

    }
    public readonly object SkipUserLock = new object();
    public readonly ConcurrentDictionary<string, bool> SkipUsers = new();
    public int SkipCount => SkipUsers.Count;
    


    // Key :한 유저, Value : 지목을 받은 유저 (만약 없다면 빈 스트링)(모든 유저가 key값으로 있음)
    public readonly ConcurrentDictionary<string, string> PointInfo = new ConcurrentDictionary<string, string>();
    // Key :한 유저, Value : 목표 아이템ID (만약 없다면 빈 스트링)(모든 유저가 key값으로 있음)
    public readonly ConcurrentDictionary<string, string> QuestInfo = new ConcurrentDictionary<string, string>();
    #endregion

    public  GameManager(GameConfig gameConfig, Room currentRoom)
    {
        
        this.currentRoom = currentRoom;
        stateMachine = new StateMachine<IUpdatableState>();
        
        
        
        startState = new GameStartState(stateMachine, this, gameConfig.stateGameStartTime);
        genreAssignAndLiarSelectState =
            new GenreAssignAndLiarSelectState(stateMachine, this, gameConfig.stateGenreAssignAndLiarSelectTime);
        keywordDistributeState = new KeywordDistributeState(stateMachine, this, gameConfig.stateKeywordDistributeTime);
        martEnterState = new MartEnterState(stateMachine, this, gameConfig.stateMartEnterTime);
        martMoveState = new MartMoveState(stateMachine, this, gameConfig.stateMartMoveTime);
        martReturnState = new MartReturnState(stateMachine, this, gameConfig.stateMartReturnTime);
        showItemAndSpeakState = new ShowItemAndSpeakState(stateMachine, this, gameConfig.stateShowItemAndSpeakTime);
        speechEndState = new SpeechEndState(stateMachine, this, gameConfig.stateSpeechEndTime);
        pointAtSuspectState = new PointAtSuspectState(stateMachine, this, gameConfig.statePointAtSuspectTime);
        pointAtSuspectEndState = new PointAtSuspectEndState(stateMachine, this, gameConfig.statePointAtSuspectEndTime);
        debateTimeState = new DebateTimeState(stateMachine, this, gameConfig.stateDebateTime);
        debateEndState = new DebateEndState(stateMachine, this, gameConfig.stateDebateEndTime);
        voteState = new VoteState(stateMachine, this, gameConfig.stateVoteTime);
        voteEndState = new VoteEndState(stateMachine, this, gameConfig.stateVoteEndTime);
        liarConfirmedState = new LiarConfirmedState(stateMachine, this, gameConfig.stateLiarConfirmedTime);
        liarKeywordGuessState = new LiarKeywordGuessState(stateMachine, this, gameConfig.stateLiarKeywordGuessTime);
        liarKeywordGuessEndState = new LiarKeywordGuessEndState(stateMachine, this, gameConfig.stateLiarKeywordGuessEndTime);
        scoreTallyState = new ScoreTallyState(stateMachine, this, gameConfig.stateScoreTallyTime);
        scoreTallyEndState = new ScoreTallyEndState(stateMachine, this, gameConfig.stateScoreTallyEndTime);
        finalResultState = new FinalResultState(stateMachine, this, gameConfig.stateFinalResultTime);
        finalResultEndState = new FinalResultEndState(stateMachine, this, gameConfig.stateFinalResultEndTime);
        liarOutButtonPressedState = new LiarOutButtonPressedState(stateMachine,this, gameConfig.stateLiarOutButtonPressedTime);
        
        stateMachine.Add(startState);
        stateMachine.Add(genreAssignAndLiarSelectState);
        stateMachine.Add(keywordDistributeState);
        stateMachine.Add(martEnterState);
        stateMachine.Add(martMoveState);
        stateMachine.Add(martReturnState);
        stateMachine.Add(showItemAndSpeakState);
        stateMachine.Add(speechEndState);
        stateMachine.Add(pointAtSuspectState);
        stateMachine.Add(pointAtSuspectEndState);
        stateMachine.Add(debateTimeState);
        stateMachine.Add(debateEndState);
        stateMachine.Add(voteState);
        stateMachine.Add(voteEndState);
        stateMachine.Add(liarConfirmedState);
        stateMachine.Add(liarKeywordGuessState);
        stateMachine.Add(liarKeywordGuessEndState);
        stateMachine.Add(scoreTallyState);
        stateMachine.Add(scoreTallyEndState);
        stateMachine.Add(finalResultState);
        stateMachine.Add(finalResultEndState);
        stateMachine.Add(liarOutButtonPressedState);
        
    }

    public void Tick()
    {
        stateMachine.Tick();
    }

    public void GameStart()
    {
        // 0 -> 1 로 바꾼 사람만 통과. 두 명이 동시에 눌러도 한 명만 들어온다.
        if (Interlocked.CompareExchange(ref isGameRunning, 1, 0) != 0) return;
        if(currentRoom.members.Count < 3)
        {
            Console.WriteLine($"[총 유저가 3명 미만] 총 유저 수 : {currentRoom.members.Count}");
            return;
        };
        Init();
        stateMachine.ChangeState<GameStartState>();
        
    }

    private void Init()
    {

        List<Room.Member> memberList = currentRoom.members.Values.ToList();
        UserGameInfos = new ConcurrentDictionary<string, UserInfo>();
        for (int i = 0; i < memberList.Count; i++)
        {
            UserGameInfos.TryAdd(memberList[i].User.Id, new UserInfo(memberList[i].User,0));
        }
        PointInfo.Clear();
        QuestInfo.Clear();
        foreach (var userInfoDic in UserGameInfos)
        {
            currentRoom.members[userInfoDic.Key].IsReady = false;
            bool s = PointInfo.TryAdd(userInfoDic.Key, "");
            bool q = QuestInfo.TryAdd(userInfoDic.Key, "");
            
            
        }
        
        
        MartItemsCategoryClear();
        SetRandomCategories();
        for (int i = 0; i < AllCategories.Length; i++)
        {
            AllMartItems.TryAdd(AllCategories[i], new ConcurrentQueue<string>());
        }


        currentSpeakedCount = 0;
        SkipUserDicClear();
        currentCycle = 0;
        currentRound = 0;
        currentCategory = AllCategories[0];
        maxSpeakedCount = UserGameInfos.Count;
        focausUser = new Protocol.User();

        OldKeyWords = new List<KeyWordDef>();
        Console.WriteLine($"[게임 초기화 완료]: 룸: {currentRoom.code}");
    }

    public void SkipUserDicClear()
    {
        lock (SkipUserLock)
        {
            SkipUsers.Clear();
        }
    }
    public void SetRandomCategories()
    {
        
        List<CategoryType> pool = DataManager.Instance.GetAllTypes();
        int need = currentRoom.GameConfig.MaxCycle;
        if (need > pool.Count)
            throw new InvalidOperationException($"MaxCycle({need})이 카테고리 수({pool.Count})보다 크다. appsettings.json 을 고쳐라.");
  
        Random rnd = new Random();
        

        AllCategories = pool.OrderBy(_ => rnd.Next()).Take(need).ToArray();
        

    }
    public void GameEnd()
    {
        // 1 -> 0 로 바꾼 경우만 통과
        if (Interlocked.CompareExchange(ref isGameRunning, 0, 1) != 1) return;
        stateMachine.StopStateMachine();
        PointInfo.Clear();
        CurrentGanre = new GenreDef();
        CurrentKeyWord  = new KeyWordDef();
        CurrentLiarKeyword = new KeyWordDef();
        OldKeyWords.Clear();
    }

    public void MartItemsCategoryClear()
    {
        foreach (var martItems in AllMartItems.Values)
        {
            martItems?.Clear();
        }
        AllMartItems.Clear();
    }
    public void MartItemsClear()
    {
        foreach (var martItems in AllMartItems.Values)
        {
            martItems?.Clear();
        }

    }

    public void SetMartItemsDicCategory()
    {
        MartItemsCategoryClear();
        for (int i = 0; i < AllCategories.Length; i++)
        {
            AllMartItems.TryAdd(AllCategories[i], new ConcurrentQueue<string>());
        }
    }
    public void RemovePlayerSelectedItemFromBag()
    {
        foreach (var categoryType in AllCategories)
        {
            List<string> list = AllMartItems[categoryType].ToList();


            foreach (var userInfo in UserGameInfos)
            {
                for (int i = 0; i < userInfo.Value.ItemIds.Length; i++)
                {
                    if (list.Contains(userInfo.Value.ItemIds[i]))
                        list.Remove(userInfo.Value.ItemIds[i]);

                }
            }

            AllMartItems[categoryType].Clear();
            for (int i = 0; i < list.Count; i++)
            {
                AllMartItems[categoryType].Enqueue(list[i]);
            }
        }
    }

    public void ChangeCategory()
    {
        int currentCategoryIndex = -1;
        for (int i = 0; i < AllCategories.Length; i++)
        {
            if (currentCategory == AllCategories[i])
            {
                currentCategoryIndex = i;
                break;
            }
        }

        if (currentCategoryIndex != -1)
        {
            currentCategory = AllCategories[(currentCategoryIndex + 1) % AllCategories.Length];
            
        }
        else
        {
            currentCategory = AllCategories.First();
        }
    }
}