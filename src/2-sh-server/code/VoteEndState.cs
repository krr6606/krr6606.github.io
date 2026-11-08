using System.Timers;
using Jay.FSM;
using NetworkManager;

namespace HelloServer.State;

public class VoteEndState : BaseGameTurnState
{


    private string result = "";
    public VoteEndState(StateMachine<IUpdatableState> stateMachine, GameManager gameManager, float MaxMsTime) : base(stateMachine, gameManager, MaxMsTime)
    {
    }

    public override void Enter()
    {
        base.Enter();

        BroadcastAsync(TurnMessageFactory.VoteEnd(MaxMsTime,gameManager.currentCycle,gameManager.currentRound));
        int count = gameManager.Votes.Count;

        List<string> resultList = new List<string>();
        List<Protocol.VoteMessage> list = gameManager.Votes.Values.ToList();
        
        for (int i = 0; i < count; i++)
        {
            if (list[i] == null)
            {
                Console.WriteLine($"[투표 저장 정보 꺼내기 실패]");
                return;
            }
            else
            {
                resultList.Add(list[i].selectNum);
            }

        }

        // 자기 자신 제외 ( -1 더해줌)
        for (int i = gameManager.UserGameInfos.Count - count - 1 - 1; i >= 0; i--)
        {

            resultList.Add(Protocol.SelectNum.DontKnow.ToString());
        }

        for (int i = 0; i < resultList.Count; i++)
        {
            Console.WriteLine($"[최종 투표 리스트] {i}번째 : {resultList[i]}");
        }
        result = GetWinner(resultList);
        Console.WriteLine($"[최종 투표 result 타입] : {result}");
    }


    public override void Tick()
    {
        base.Tick();
        if (currentMsTime > MaxMsTime)
        {

            if (result != Protocol.SelectNum.Liar.ToString() &&
                gameManager.currentCycle >= gameManager.currentRoom.GameConfig.MaxCycle)
            {
                stateMachine.ChangeState<PointAtSuspectState>();
            }
            else if(result != Protocol.SelectNum.Liar.ToString() &&
                    gameManager.currentCycle < gameManager.currentRoom.GameConfig.MaxCycle)
            {
                stateMachine.ChangeState<ShowItemAndSpeakState>();
            }
            else if(result == Protocol.SelectNum.Liar.ToString())
            {
                stateMachine.ChangeState<LiarConfirmedState>();
            }
        }
    }
    private string GetWinner(List<string> list)
    {
        string result = "";
        // 데이터가 없으면 빈 값 반환
        if (list == null || list.Count == 0) return "";

        Dictionary<string, int> dict = new Dictionary<string, int>();
        foreach (var s in list)
        {
            if (dict.ContainsKey(s) == false)
            {
                dict.Add(s, 1);
            }
            else
            {
                dict[s]++;
            }
        }

        
        List<KeyValuePair<string, int>> orderedList = dict.OrderByDescending(x => x.Value).ToList();
        
        // 만약 라이어와 
        if (orderedList.Count > 1 && orderedList[0].Value == orderedList[1].Value
                                  && ((orderedList[0].Key == Protocol.SelectNum.DontKnow.ToString() && orderedList[1].Key == Protocol.SelectNum.Liar.ToString())
                                      || orderedList[1].Key == Protocol.SelectNum.DontKnow.ToString() && orderedList[0].Key == Protocol.SelectNum.Liar.ToString()))
        {
            result = Protocol.SelectNum.Liar.ToString();
        }
        else if (orderedList.Count > 1 && orderedList[0].Value == orderedList[1].Value)
        {
            result = "";
        }
        else if (orderedList[0].Key == Protocol.SelectNum.NotLiar.ToString())
        {
            result = Protocol.SelectNum.NotLiar.ToString();
        }
        else if (orderedList[0].Key == Protocol.SelectNum.DontKnow.ToString())
        {
            result = Protocol.SelectNum.DontKnow.ToString();
        }
        else if (orderedList[0].Key == Protocol.SelectNum.Liar.ToString())
        {
            result = Protocol.SelectNum.Liar.ToString();
        }
        // 동률인 경우 빈 값 반환
        return result;
    }
}