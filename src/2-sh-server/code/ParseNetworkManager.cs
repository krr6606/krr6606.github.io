namespace HelloServer;
using System;
using System.Text.Json;
using Jay;
public class ParseNetworkManager
{


/// <summary>
/// 서버가 보낸 TurnMessage / ChatMessage / UserInteractionMessage를 해석해서
/// 등록된 정적 Action을 실행하는 클라이언트 매니저.
/// !주의사항! 함수를 등록하고 꼭 게임이 끝나거나 함수가 있는 객체가 사라지는 시점에 -= 으로 할당 해제해주기!
/// </summary>
public static class NetworkManager 
{
    // ============================================================
    // ===== TurnMessage 이벤트 (전부 TimerMs, Cycle, Round 선행) =====
    // ============================================================

    public static Action<float, int, int> OnGameStartState;

    // GenreId, CurrentOwnerID
    public static Action<float, int, int, int, string> OnGenreAssignAndLiarSelectState;

    // KeywordId
    public static Action<float, int, int, int> OnKeywordDistributeState;

    // QuestId
    public static Action<float, int, int, string, Protocol.CategoryItemArray[]> OnMartEnterState;

    public static Action<float, int, int> OnMartMoveState;

    // userScoreInfo
    public static Action<float, int, int, Protocol.UserItemList[], Protocol.UserQuestInfo[]> OnMartReturnState;

    // CurrentFocusID, CurrentCategory
    public static Action<float, int, int, string, string> OnShowItemAndSpeakState;

    public static Action<float, int, int> OnSpeechEndState;

    public static Action<float, int, int> OnPointAtSuspectState;

    // CurrentFocusID (최다 득표자, 동률/없음이면 빈 문자열)
    public static Action<float, int, int, string> OnPointAtSuspectEndState;

    public static Action<float, int, int> OnDebateTimeState;
    public static Action<float, int, int> OnDebateEndState;

    public static Action<float, int, int> OnVoteState;
    public static Action<float, int, int> OnVoteEndState;

    // CurrentFocusID (확정된 라이어)
    public static Action<float, int, int, string> OnLiarConfirmedState;

    public static Action<float, int, int> OnLiarKeywordGuessState;

    // liarKeyword, nomalKeyword, IsRightAnswer, userScoreInfo
    public static Action<float, int, int, string, string, bool, Protocol.UserScoreInfo[]> OnLiarKeywordGuessEndState;

    // LiarOutButtonInfo, userScoreInfo
    public static Action<float, int, int, string[], Protocol.UserScoreInfo[]> OnScoreTallyState;

    public static Action<float, int, int> OnScoreTallyEndState;

    // CurrentOwnerIDs
    public static Action<float, int, int, string[]> OnFinalResultState;

    public static Action<float, int, int> OnFinalResultEndState;
    
    // CurrentFocusID (라이어 버튼을 누른 라이어)
    public static Action<float, int, int, string> OnLiarOutButtonPressedState;
    
    // ============================================================
    // ===== ChatMessage 이벤트 (ID, NickName, Text) =====
    // ============================================================

    public static Action<string, string, string> OnNormalChat;
    public static Action<string, string, string> OnSpecialChat;
    public static Action<string, string, string> OnKeywordGuessChat;

    // ============================================================
    // ===== UserInteractionMessage 이벤트 (IsValid, senderId, receivedId) =====
    // ============================================================

    public static Action<bool,bool, string, string> OnPushQuery;
    public static Action<bool,bool, string, string> OnPushAnswer;
    public static Action<bool,bool, string, string> OnItemHoldQuery;
    public static Action<bool,bool, string, string> OnItemHoldAnswer;
    public static Action<bool,bool, string, string> OnItemDropQuery;
    public static Action<bool,bool, string, string> OnItemDropAnswer;
    public static Action<bool,bool, string, string,string> OnItemPutInBagQuery;
    public static Action<bool,bool, string, string,string> OnItemPutInBagAnswer;



    // ============================================================
    // ===== 메시지 해석 진입점 (메시지 종류별 오버로드) =====
    // ============================================================

    public static void MessageParse(Protocol.TurnMessage msg)
    {
        switch (msg.TurnMessageType)
        {
            case Protocol.TurnMessageType.GameStartState:
                OnGameStartState?.Invoke(msg.TimerMs, msg.CurrentCycle, msg.CurrentRound);
                break;

            case Protocol.TurnMessageType.GenreAssignAndLiarSelectState:
            {
                var p = Parse<Protocol.GenreAssignAndLiarSelectParameter>(msg.Parameter);
                OnGenreAssignAndLiarSelectState?.Invoke(msg.TimerMs, msg.CurrentCycle, msg.CurrentRound,
                    p.GenreId, p.CurrentOwnerID);
                break;
            }

            case Protocol.TurnMessageType.KeywordDistributeState:
            {
                var p = Parse<Protocol.KeywordDistributeParameter>(msg.Parameter);
                OnKeywordDistributeState?.Invoke(msg.TimerMs, msg.CurrentCycle, msg.CurrentRound, p.KeywordId);
                break;
            }

            case Protocol.TurnMessageType.MartEnterState:
            {
                var p = Parse<Protocol.MartEnterParameter>(msg.Parameter);
                OnMartEnterState?.Invoke(msg.TimerMs, msg.CurrentCycle, msg.CurrentRound, p.TargetItemId,p.AllCategoryItemArrays);
                break;
            }

            case Protocol.TurnMessageType.MartMoveState:
                OnMartMoveState?.Invoke(msg.TimerMs, msg.CurrentCycle, msg.CurrentRound);
                break;

            case Protocol.TurnMessageType.MartReturnState:
            {
                var p = Parse<Protocol.MartReturnParameter>(msg.Parameter);
                OnMartReturnState?.Invoke(msg.TimerMs, msg.CurrentCycle, msg.CurrentRound, p.UserItemList, p.UserQuestInfo);
                break;
            }

            case Protocol.TurnMessageType.ShowItemAndSpeakState:
            {
                var p = Parse<Protocol.ShowItemAndSpeakParameter>(msg.Parameter);
                OnShowItemAndSpeakState?.Invoke(msg.TimerMs, msg.CurrentCycle, msg.CurrentRound,
                    p.CurrentFocusID, p.CurrentCategory);
                break;
            }

            case Protocol.TurnMessageType.SpeechEndState:
                OnSpeechEndState?.Invoke(msg.TimerMs, msg.CurrentCycle, msg.CurrentRound);
                break;

            case Protocol.TurnMessageType.PointAtSuspectState:
                OnPointAtSuspectState?.Invoke(msg.TimerMs, msg.CurrentCycle, msg.CurrentRound);
                break;

            case Protocol.TurnMessageType.PointAtSuspectEndState:
            {
                var p = Parse<Protocol.FocusIdParameter>(msg.Parameter);
                OnPointAtSuspectEndState?.Invoke(msg.TimerMs, msg.CurrentCycle, msg.CurrentRound, p.CurrentFocusID);
                break;
            }

            case Protocol.TurnMessageType.DebateTimeState:
                OnDebateTimeState?.Invoke(msg.TimerMs, msg.CurrentCycle, msg.CurrentRound);
                break;

            case Protocol.TurnMessageType.DebateEndState:
                OnDebateEndState?.Invoke(msg.TimerMs, msg.CurrentCycle, msg.CurrentRound);
                break;

            case Protocol.TurnMessageType.VoteState:
                OnVoteState?.Invoke(msg.TimerMs, msg.CurrentCycle, msg.CurrentRound);
                break;

            case Protocol.TurnMessageType.VoteEndState:
                OnVoteEndState?.Invoke(msg.TimerMs, msg.CurrentCycle, msg.CurrentRound);
                break;

            case Protocol.TurnMessageType.LiarConfirmedState:
            {
                var p = Parse<Protocol.FocusIdParameter>(msg.Parameter);
                OnLiarConfirmedState?.Invoke(msg.TimerMs, msg.CurrentCycle, msg.CurrentRound, p.CurrentFocusID);
                break;
            }

            case Protocol.TurnMessageType.LiarKeywordGuessState:
                OnLiarKeywordGuessState?.Invoke(msg.TimerMs, msg.CurrentCycle, msg.CurrentRound);
                break;

            case Protocol.TurnMessageType.LiarKeywordGuessEndState:
            {
                var p = Parse<Protocol.LiarKeywordGuessEndParameter>(msg.Parameter);
                OnLiarKeywordGuessEndState?.Invoke(msg.TimerMs, msg.CurrentCycle, msg.CurrentRound,
                    p.liarKeyword, p.nomalKeyword, p.IsRightAnswer, p.userScoreInfo);
                break;
            }

            case Protocol.TurnMessageType.ScoreTallyState:
            {
                var p = Parse<Protocol.ScoreTallyParameter>(msg.Parameter);
                OnScoreTallyState?.Invoke(msg.TimerMs, msg.CurrentCycle, msg.CurrentRound,
                    p.LiarOutButtonInfo, p.userScoreInfo);
                break;
            }

            case Protocol.TurnMessageType.ScoreTallyEndState:
                OnScoreTallyEndState?.Invoke(msg.TimerMs, msg.CurrentCycle, msg.CurrentRound);
                break;

            case Protocol.TurnMessageType.FinalResultState:
            {
                var p = Parse<Protocol.FinalResultParameter>(msg.Parameter);
                OnFinalResultState?.Invoke(msg.TimerMs, msg.CurrentCycle, msg.CurrentRound, p.CurrentOwnerIDs);
                break;
            }

            case Protocol.TurnMessageType.FinalResultEndState:
                OnFinalResultEndState?.Invoke(msg.TimerMs, msg.CurrentCycle, msg.CurrentRound);
                break;

            case Protocol.TurnMessageType.LiarOutButtonPressedState:
            {
                var p = Parse<Protocol.FocusIdParameter>(msg.Parameter);
                OnLiarOutButtonPressedState?.Invoke(msg.TimerMs, msg.CurrentCycle, msg.CurrentRound,p.CurrentFocusID);
                break;
                
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public static void MessageParse(Protocol.ChatMessage msg)
    {
        switch (msg.ChatType)
        {
            case Protocol.ChatType.Normal:
                OnNormalChat?.Invoke(msg.ID, msg.NickName, msg.Text);
                break;

            case Protocol.ChatType.Special:
                OnSpecialChat?.Invoke(msg.ID, msg.NickName, msg.Text);
                break;

            case Protocol.ChatType.KeywordGuess:
                OnKeywordGuessChat?.Invoke(msg.ID, msg.NickName, msg.Text);
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public static void MessageParse(Protocol.UserInteractionMessage msg)
    {
        switch (msg.InteractionType)
        {
            case Protocol.InteractionType.PushQuery:
                OnPushQuery?.Invoke(msg.IsValid,msg.IsSuccess, msg.senderId, msg.receivedId);
                break;
            case Protocol.InteractionType.PushAnswer:
                OnPushAnswer?.Invoke(msg.IsValid,msg.IsSuccess, msg.senderId, msg.receivedId);
                break;
            case Protocol.InteractionType.ItemHoldQuery:
                OnItemHoldQuery?.Invoke(msg.IsValid,msg.IsSuccess, msg.senderId, msg.receivedId);
                break;
            case Protocol.InteractionType.ItemHoldAnswer:
                OnItemHoldAnswer?.Invoke(msg.IsValid,msg.IsSuccess, msg.senderId, msg.receivedId);
                break;
            case Protocol.InteractionType.ItemDropQuery:
                OnItemDropQuery?.Invoke(msg.IsValid,msg.IsSuccess, msg.senderId, msg.receivedId);
                break;
            case Protocol.InteractionType.ItemDropAnswer:
                OnItemDropAnswer?.Invoke(msg.IsValid,msg.IsSuccess, msg.senderId, msg.receivedId);
                break;

            case Protocol.InteractionType.ItemPutInBagQuery:
            {
                var  p = Parse<Protocol.ItemPutInBagParameter>(msg.Parameter);
                OnItemPutInBagQuery?.Invoke(msg.IsValid,msg.IsSuccess, msg.senderId, msg.receivedId,p?.ChangedItemId ?? "");
                break;
            }


            case Protocol.InteractionType.ItemPutInBagAnswer:
            {
                var p = Parse<Protocol.ItemPutInBagParameter>(msg.Parameter);
                OnItemPutInBagAnswer?.Invoke(msg.IsValid,msg.IsSuccess, msg.senderId, msg.receivedId,p?.ChangedItemId ?? "");
                break;
            }


            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static T Parse<T>(string json) where T : class
    {
        if (string.IsNullOrEmpty(json)) return null;
        return JsonSerializer.Deserialize<T>(json);
    }
}
}