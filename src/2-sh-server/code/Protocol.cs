
using System.Text.Json;

namespace HelloServer
{

    public class Protocol
    {
        [Serializable]
        public enum TurnMessageType
        {
            // ===== 게임 시작 처리 =====
            GameStartState, // 게임 시작 상태
            GenreAssignAndLiarSelectState, // 장르 배분 및 라이어 선정 상태
            KeywordDistributeState, // 키워드 뿌리기 상태

            // ===== 마트 처리 =====
            MartEnterState, // 마트 진입 상태
            MartMoveState, // 마트 이동 상태
            MartReturnState, // 마트에서 복귀 상태

            // ===== 턴 처리: 발언 =====
            ShowItemAndSpeakState, // 물건 보여주고 발언 상태
            SpeechEndState, // 발언 종료 상태

            // ===== 턴 처리: 지목 =====
            PointAtSuspectState, // 지목 상태
            PointAtSuspectEndState, // 지목 종료 상태

            // ===== 턴 처리: 변론 =====
            DebateTimeState, // 변론 시간 상태
            DebateEndState, // 변론 종료 상태

            // ===== 턴 처리: 투표 =====
            VoteState, // 투표 상태
            VoteEndState, // 투표 종료 상태

            // ===== 라이어 확정 및 키워드 맞추기 =====
            LiarConfirmedState, // 라이어 확정 상태
            LiarKeywordGuessState, // 라이어의 키워드 맞춤 상태
            LiarKeywordGuessEndState, // 키워드 맞춤 종료 상태

            // ===== 점수 집계 / 라운드 종료 =====
            ScoreTallyState, // 점수 집계 상태
            ScoreTallyEndState, // 점수 집계 종료 상태
            FinalResultState, // 최종 결과 상태
            FinalResultEndState, // 최종 결과 종료 상태

            // 라이어가 라이어 버튼을 눌러 진입하는 특수 턴
            LiarOutButtonPressedState, // 라밍아웃 버튼 누름 상태
        }

        [Serializable]
        public enum InteractionType
        {
            PushQuery,
            PushAnswer,
            ItemHoldQuery,
            ItemHoldAnswer,
            ItemDropQuery,
            ItemDropAnswer,
            ItemPutInBagQuery,
            ItemPutInBagAnswer,
        }

        #region 턴 추상화와 파라미터들

        [Serializable]
        public class TurnMessage
        {
            public string Type { get; set; } = "turnMessage";
            public TurnMessageType TurnMessageType { get; set; }
            public float TimerMs { get; set; } // 모든 타임은 MS로 들어옴
            public int CurrentCycle { get; set; }
            public int CurrentRound { get; set; }

            // 타입별로 달라지는 나머지 필드 (없을 수도, JSON 객체 하나일 수도 있음)
            public string Parameter { get; set; }

            /// <summary>Parameter를 원하는 파라미터 클래스 타입으로 역직렬화. 없으면 null.</summary>
            public T GetParameter<T>() where T : class
            {
                if (string.IsNullOrEmpty(Parameter)) return null;
                return JsonSerializer.Deserialize<T>(Parameter);
            }
        }

        // ============================================================
        // 타입별 파라미터 클래스들
        // (파라미터가 없는 타입은 별도 Paramerter 없이 Parameter == null로 처리)
        // ============================================================

        [Serializable]
        public class GenreAssignAndLiarSelectParameter
        {
            public int GenreId { get; set; } // 장르
            public string CurrentOwnerID { get; set; } // 선정된 라이어
        }

        [Serializable]
        public class KeywordDistributeParameter
        {
            public int KeywordId { get; set; } // 라이어는 라이어 키워드, 나머지는 일반
        }

        [Serializable]
        public class MartEnterParameter
        {
            public CategoryItemArray[] AllCategoryItemArrays{ get; set; }
            public string TargetItemId { get; set; }
            
        }

        [Serializable]
        public class MartReturnParameter
        {
            
           public UserQuestInfo[] UserQuestInfo { get; set; }
           public UserItemList[] UserItemList { get; set; }
            
        }
        [Serializable]
        public class UserQuestInfo
        {
            public string UserId { get; set; }
            public bool IsSuccess { get; set; }
        }
        [Serializable]
        public class UserItemList
        {
            public string UserId { get; set; }
            public string[] ItemList { get; set; }
            
        }
        [Serializable]
        public class ShowItemAndSpeakParameter
        {
            public string CurrentFocusID { get; set; } // 현재 발언 차례인 유저
            public string CurrentCategory { get; set; } // 현재 사이클의 아이템 카테고리
        }

        /// <summary>
        /// "누구 한 명"만 필요한 상태들(지목 종료, 라이어 확정, 라이어가 라밍아웃 버튼 누를 시)이 공용으로 쓰는 파라미터.
        /// </summary>
        [Serializable]
        public class FocusIdParameter
        {
            public string CurrentFocusID { get; set; }
        }
        
        [Serializable]
        public class LiarKeywordGuessEndParameter
        {
            public string liarKeyword { get; set; }
            public string nomalKeyword { get; set; }
            public bool IsRightAnswer { get; set; }
            public UserScoreInfo[] userScoreInfo { get; set; }
        }

        [Serializable]
        public class ScoreTallyParameter
        {
            public string[] LiarOutButtonInfo { get; set; } // 라이어가 아닌데 버튼 누른 유저들
            public UserScoreInfo[] userScoreInfo { get; set; }
        }

        [Serializable]
        public class FinalResultParameter
        {
            public string[] CurrentOwnerIDs { get; set; } // 최종 승자 ID (동률 시 여러 명)
        }


        #endregion

        #region 유저의 인터렉티브 추상화와 파라미터들

        //
        [Serializable]
        public class UserInteractionMessage
        {
            public string Type { get; set; } = "userInteraction";
            public InteractionType InteractionType { get; set; }
            public bool IsValid { get; set; } // 레이스 컨디션 등, 실패시 false 전달
            public bool IsSuccess { get; set; }
            public string senderId { get; set; }
            public string receivedId { get; set; }
            public string Parameter { get; set; }

            /// <summary>Parameter를 원하는 파라미터 클래스 타입으로 역직렬화. 없으면 null.</summary>
            public T GetParameter<T>() where T : class
            {
                if (string.IsNullOrEmpty(Parameter)) return null;
                return JsonSerializer.Deserialize<T>(Parameter);
            }
        }

        //서버에서 게산후 담아 보내줌

        [Serializable]
        public class ItemPutInBagParameter
        {

            public string ChangedItemId{ get; set; } // 비어있으면 교체할 필요 없고, 있으면 교체되야하는 가방에 들어있던 아이템 ID

        }

        #endregion



        #region 공용 데이터 처리

        [Serializable]
        public enum SelectNum
        {
            Liar,
            DontKnow,
            NotLiar,
        }

        #region 채팅 처리

        [Serializable]
        public enum ChatType
        {
            Normal,
            Special,
            KeywordGuess,
        }

        [Serializable]
        public class ChatMessage
        {
            public string Type { get; set; } = "chat";
            public ChatType ChatType { get; set; }
            public string ID { get; set; }
            public string NickName { get; set; }
            public string Text { get; set; }
            public string Parameter { get; set; }

            /// <summary>Parameter를 원하는 파라미터 클래스 타입으로 역직렬화. 없으면 null.</summary>
            public T GetParameter<T>() where T : class
            {
                if (string.IsNullOrEmpty(Parameter)) return null;
                return JsonSerializer.Deserialize<T>(Parameter);
            }
        }

        #endregion

        #region 이동 처리

        [Serializable]
        public class MoveMessage
        {
            public string Type { get; set; } = "move";
            public string Id { get; set; }
            public float X { get; set; }
            public float Y { get; set; }

            public float Z { get; set; }
        }

        #endregion

        #endregion


        [Serializable]
        public class TypeOnly
        {
            public string Type { get; set; }
        }

        [Serializable]
        public class NewGameConfig
        {
            public int MaxRound { get; set; }
            public int MaxCycle { get; set; }
        }

        [Serializable]
        public class CategoryItemArray
        {
            public CategoryType Category { get; set; }
            public string[] ItemIds { get; set; }
        }

        #region 기본 상태 처리

        [Serializable]
        public class StateMessage
        {
            public string Type { get; set; } = "state";
            public PlayerState[] States { get; set; }
        }


        [Serializable]
        public class UserScoreInfo
        {
            public string UserId { get; set; }
            public int UserScore { get; set; }
        }


        [Serializable]
        public class PlayerState // 지속적으로 클라이언트에게 뿌려주는 데이터
        {
            public string Id { get; set; }
            public float X { get; set; }
            public float Y { get; set; }

            // 추가 코드
            public float Z { get; set; }
            public bool IsLiar { get; set; } // 라이어 인가?

            

        }




        #endregion

        #region 유저 입장/퇴장 처리

        [Serializable]
        public class User // room에 들어와을 때 서버-> 클라 한번만 뿌려준다
        {
            public string Id { get; set; }
            public string NickName { get; set; }
        }

        // 누가 새로 들어왔다.
        [Serializable]
        public class JoinMessage
        {
            public string Type { get; set; } = "join";
            public User User { get; set; }
        }

        [Serializable]
        public class WelcomeMessage
        {
            public string Type { get; set; } = "welcome";

            public string RoomCode { get; set; }

            public User User { get; set; }
            public User[] Users { get; set; }
            public int ReadyCount { get; set; }
        }

        // 누가 나갔다.
        [Serializable]
        public class LeaveMessage
        {
            public string Type { get; set; } = "leave";
            public string Id { get; set; }
        }

        #endregion

        [Serializable]
        public class GameStartOKMessage
        {
            public string Type { get; set; } = "게임 시작 확인";
            public NewGameConfig newGameConfig { get; set; }
        }


        // (특수)라밍아웃 처리
        [Serializable]
        public class LiarSelfDisclose
        {
            public string Type = "LiarSelfDisclose";
            public string ID;
        }



        #region 클라이언트 -> 서버

        #region 유저 입장/퇴장 처리

        [Serializable]
        public class HelloMessage
        {
            public string Type { get; set; } = "hello";
            public string NickName { get; set; }
        }

        #endregion

        #region 게임 시작 처리

        [Serializable]
        public class ReadyMessage
        {
            public string Type { get; set; } = "ready";
            public string ID { get; set; }
            public int readyCount { get; set; }
        }

        [Serializable]
        public class AllReadyMessage
        {
            public string Type { get; set; } = "AllReady";
        }
        
        #endregion

        #region 투표 처리

        // 선택
        [Serializable]
        public class SelectMessage
        {
            public string Type { get; set; } = "Select";

            public string selectID { get; set; } // 선택한 유저 ID
            public string selectedID { get; set; } // 선택된 유저 ID

            public bool IsSelectCancel { get; set; } // 선택 취소 여부 bool값
        }

        // 빠른 스킵
        [Serializable]
        public class NonPointMessage
        {
            public string Type { get; set; } = "NonPoint";
            public string UserID { get; set; } // 투표 안하는 유저 ID
        }

        // 투표
        [Serializable]
        public class VoteMessage
        {
            public string Type { get; set; } = "Vote";
            public string UserID { get; set; } // 투표하는 유저 ID
            public string selectNum { get; set; } // 라이어가 아니다 or 모르겠다 or 라이어다
        }

        #endregion

        #region 마트 부분




        [Serializable]
        public class PushAnimationMessage
        {
            public string Type { get; set; } = "PushAnimation";
            public string UserID { get; set; }
        }



        #endregion


        #endregion

    }
}