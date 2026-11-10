using System.Text.Json;

using HelloServer;

namespace NetworkManager
{

    /// <summary>
    /// 새 상태가 추가될 때 할 일:
    ///   1) TurnMessageType에 값 추가
    ///   2) 파라미터가 있으면 parameter 클래스 추가 (TurnMessage.cs, Common)
    ///   3) 여기에 빌더 메서드 하나 추가
    /// </summary>
    public static class TurnMessageFactory
    {
        /// <summary>파라미터가 없는 상태 메시지 공용 빌더.</summary>
        public static Protocol.TurnMessage CreateSimple(Protocol.TurnMessageType type, float timerMs, int cycle,
            int round)
        {
            return new Protocol.TurnMessage
            {
                TurnMessageType = type,
                TimerMs = timerMs,
                CurrentCycle = cycle,
                CurrentRound = round,
                Parameter = null
            };
        }

        /// <summary>파라미터가 있는 상태 메시지 공용 빌더 (Payload를 JSON으로 직렬화).</summary>
        private static Protocol.TurnMessage Create<T>(Protocol.TurnMessageType type, float timerMs, int cycle,
            int round, T parameter)
        {
            return new Protocol.TurnMessage
            {
                TurnMessageType = type,
                TimerMs = timerMs,
                CurrentCycle = cycle,
                CurrentRound = round,
                Parameter = JsonSerializer.Serialize(parameter)
            };
        }

        // ===== 게임 시작 처리 =====
        public static Protocol.TurnMessage GameStart(float timerMs, int cycle, int round)
            => CreateSimple(Protocol.TurnMessageType.GameStartState, timerMs, cycle, round);

        public static Protocol.TurnMessage GenreAssignAndLiarSelect(float timerMs, int cycle, int round, int genreId,
            string liarId)
            => Create(Protocol.TurnMessageType.GenreAssignAndLiarSelectState, timerMs, cycle, round,
                new Protocol.GenreAssignAndLiarSelectParameter { GenreId = genreId, CurrentOwnerID = liarId });

        public static Protocol.TurnMessage KeywordDistribute(float timerMs, int cycle, int round, int keywordId)
            => Create(Protocol.TurnMessageType.KeywordDistributeState, timerMs, cycle, round,
                new Protocol.KeywordDistributeParameter { KeywordId = keywordId });

        // ===== 마트 처리 =====
        public static Protocol.TurnMessage MartEnter(float timerMs, int cycle, int round, string targetItemId,Protocol.CategoryItemArray[] categories)
            => Create(Protocol.TurnMessageType.MartEnterState, timerMs, cycle, round,
                new Protocol.MartEnterParameter { TargetItemId = targetItemId, AllCategoryItemArrays =  categories });

        public static Protocol.TurnMessage MartMove(float timerMs, int cycle, int round)
            => CreateSimple(Protocol.TurnMessageType.MartMoveState, timerMs, cycle, round);

        public static Protocol.TurnMessage MartReturn(float timerMs, int cycle, int round,
            Protocol.UserItemList[] userItemLists, Protocol.UserQuestInfo[]  userQuestInfos)
            => Create(Protocol.TurnMessageType.MartReturnState, timerMs, cycle, round,
                new Protocol.MartReturnParameter { 
                    UserItemList = userItemLists, 
                    UserQuestInfo = userQuestInfos});

        // ===== 턴 처리: 발언 =====
        public static Protocol.TurnMessage ShowItemAndSpeak(float timerMs, int cycle, int round, string ownerId,
            string category)
            => Create(Protocol.TurnMessageType.ShowItemAndSpeakState, timerMs, cycle, round,
                new Protocol.ShowItemAndSpeakParameter { CurrentFocusID = ownerId, CurrentCategory = category });

        public static Protocol.TurnMessage SpeechEnd(float timerMs, int cycle, int round)
            => CreateSimple(Protocol.TurnMessageType.SpeechEndState, timerMs, cycle, round);

        // ===== 턴 처리: 지목 =====
        public static Protocol.TurnMessage PointAtSuspect(float timerMs, int cycle, int round)
            => CreateSimple(Protocol.TurnMessageType.PointAtSuspectState, timerMs, cycle, round);

        public static Protocol.TurnMessage PointAtSuspectEnd(float timerMs, int cycle, int round,
            string mostPointedId)
            => Create(Protocol.TurnMessageType.PointAtSuspectEndState, timerMs, cycle, round,
                new Protocol.FocusIdParameter { CurrentFocusID = mostPointedId });

        // ===== 턴 처리: 변론 =====
        public static Protocol.TurnMessage DebateTime(float timerMs, int cycle, int round)
            => CreateSimple(Protocol.TurnMessageType.DebateTimeState, timerMs, cycle, round);

        public static Protocol.TurnMessage DebateEnd(float timerMs, int cycle, int round)
            => CreateSimple(Protocol.TurnMessageType.DebateEndState, timerMs, cycle, round);

        // ===== 턴 처리: 투표 =====
        public static Protocol.TurnMessage Vote(float timerMs, int cycle, int round)
            => CreateSimple(Protocol.TurnMessageType.VoteState, timerMs, cycle, round);

        public static Protocol.TurnMessage VoteEnd(float timerMs, int cycle, int round)
            => CreateSimple(Protocol.TurnMessageType.VoteEndState, timerMs, cycle, round);

        // ===== 라이어 확정 및 키워드 맞추기 =====
        public static Protocol.TurnMessage LiarConfirmed(float timerMs, int cycle, int round, string liarId)
            => Create(Protocol.TurnMessageType.LiarConfirmedState, timerMs, cycle, round,
                new Protocol.FocusIdParameter { CurrentFocusID = liarId });

        public static Protocol.TurnMessage LiarKeywordGuess(float timerMs, int cycle, int round)
            => CreateSimple(Protocol.TurnMessageType.LiarKeywordGuessState, timerMs, cycle, round);

        public static Protocol.TurnMessage LiarKeywordGuessEnd(float timerMs, int cycle, int round,
            string liarKeyword, string normalKeyword, bool isRight, Protocol.UserScoreInfo[] scores)
            => Create(Protocol.TurnMessageType.LiarKeywordGuessEndState, timerMs, cycle, round,
                new Protocol.LiarKeywordGuessEndParameter
                {
                    liarKeyword = liarKeyword,
                    nomalKeyword = normalKeyword,
                    IsRightAnswer = isRight,
                    userScoreInfo = scores
                });

        // ===== 점수 집계 / 라운드 종료 =====
        public static Protocol.TurnMessage ScoreTally(float timerMs, int cycle, int round,
            string[] liarOutButtonUsers, Protocol.UserScoreInfo[] scores)
            => Create(Protocol.TurnMessageType.ScoreTallyState, timerMs, cycle, round,
                new Protocol.ScoreTallyParameter { LiarOutButtonInfo = liarOutButtonUsers, userScoreInfo = scores });

        public static Protocol.TurnMessage ScoreTallyEnd(float timerMs, int cycle, int round)
            => CreateSimple(Protocol.TurnMessageType.ScoreTallyEndState, timerMs, cycle, round);

        public static Protocol.TurnMessage FinalResult(float timerMs, int cycle, int round, string[] winnerIds)
            => Create(Protocol.TurnMessageType.FinalResultState, timerMs, cycle, round,
                new Protocol.FinalResultParameter { CurrentOwnerIDs = winnerIds });

        public static Protocol.TurnMessage FinalResultEnd(float timerMs, int cycle, int round)
            => CreateSimple(Protocol.TurnMessageType.FinalResultEndState, timerMs, cycle, round);

        public static Protocol.TurnMessage LiarOutButtonPressed(float timerMs, int cycle, int round, string liarId)
            => Create(Protocol.TurnMessageType.LiarOutButtonPressedState, timerMs, cycle, round,
                new Protocol.FocusIdParameter { CurrentFocusID = liarId });

    }

    public static class ChatMessageFactory
    {
        /// <summary>파라미터가 없는 상태 메시지 공용 빌더.</summary>
        public static Protocol.ChatMessage CreateSimple(Protocol.ChatType type, string id
            ,string NickName, string Text)
        {
            return new Protocol.ChatMessage
            {
                ChatType =  type,
                ID =  id,
                NickName =  NickName,
                Text =  Text
                
            };
        }

        /// <summary>파라미터가 있는 상태 메시지 공용 빌더 (Payload를 JSON으로 직렬화).</summary>
        private static Protocol.ChatMessage Create<T>(Protocol.ChatType type, string id
            ,string NickName, string Text, T parameter)
        {
            return new Protocol.ChatMessage
            {
                ChatType =  type,
                ID =  id,
                NickName =  NickName,
                Text =  Text,
                Parameter = JsonSerializer.Serialize(parameter)
            };
        }

        public static Protocol.ChatMessage NormalChat(string id
            ,string NickName, string Text)
            => CreateSimple(Protocol.ChatType.Normal, id, NickName, Text);

        public static Protocol.ChatMessage SpecialChat(string id
            ,string NickName, string Text)
            => CreateSimple(Protocol.ChatType.Special, id, NickName, Text);
        public static Protocol.ChatMessage KeywordGuessChat( string id
            ,string NickName, string Text)
            => CreateSimple(Protocol.ChatType.KeywordGuess, id, NickName, Text);
    }

    public static class InteractionMessageFactory
    {
        /// <summary>파라미터가 없는 상태 메시지 공용 빌더.</summary>
        public static Protocol.UserInteractionMessage CreateSimple(Protocol.InteractionType type, bool IsValid
      , bool isSuccess, string senderId, string receivedId)
        {
            return new Protocol.UserInteractionMessage
            {
                InteractionType = type,
                IsValid = IsValid,
                IsSuccess = isSuccess,
                senderId = senderId,
                receivedId = receivedId

            };
        }

        /// <summary>파라미터가 있는 상태 메시지 공용 빌더 (Payload를 JSON으로 직렬화).</summary>
        private static Protocol.UserInteractionMessage Create<T>(Protocol.InteractionType type, bool IsValid
            , bool isSuccess , string senderId, string receivedId, T parameter)
        {
            return new Protocol.UserInteractionMessage
            {
                InteractionType = type,
                IsValid = IsValid,
                IsSuccess = isSuccess,
                senderId = senderId,
                receivedId = receivedId,
                Parameter = JsonSerializer.Serialize(parameter)
            };
        }
        public static Protocol.UserInteractionMessage PushQuery(Protocol.InteractionType type, bool IsValid
       , bool IsSuccess, string senderId, string receivedId)
            => CreateSimple(type, IsValid, IsSuccess,senderId, receivedId);
        public static Protocol.UserInteractionMessage PushAnswer(Protocol.InteractionType type, bool IsValid
            , bool IsSuccess , string senderId, string receivedId)
            => CreateSimple(type,IsValid, IsSuccess, senderId, receivedId);
        public static Protocol.UserInteractionMessage ItemHoldQuery(Protocol.InteractionType type, bool IsValid
            , bool IsSuccess , string senderId, string receivedId)
            => CreateSimple(type, IsValid,IsSuccess, senderId, receivedId);
        public static Protocol.UserInteractionMessage ItemHoldAnswer(Protocol.InteractionType type, bool IsValid
            , bool IsSuccess , string senderId, string receivedId)
            => CreateSimple(type,IsValid, IsSuccess, senderId, receivedId);
        public static Protocol.UserInteractionMessage ItemDropQuery(Protocol.InteractionType type, bool IsValid
            , bool IsSuccess, string senderId, string receivedId)
            => CreateSimple(type, IsValid, IsSuccess,senderId, receivedId);
        public static Protocol.UserInteractionMessage ItemDropAnswer(Protocol.InteractionType type, bool IsValid
            , bool IsSuccess, string senderId, string receivedId)
            => CreateSimple(type, IsValid, IsSuccess,senderId, receivedId);
        public static Protocol.UserInteractionMessage ItemPutInBagQuery(Protocol.InteractionType type, bool IsValid
            , bool IsSuccess, string senderId, string receivedId,string changedItemId)
            => Create(type,IsValid, IsSuccess, senderId, receivedId,
                new Protocol.ItemPutInBagParameter { ChangedItemId =  changedItemId });
        public static Protocol.UserInteractionMessage ItemPutInBagAnswer(Protocol.InteractionType type, bool IsValid
            , bool IsSuccess, string senderId, string receivedId,string changedItemId)
            => Create(type, IsValid, IsSuccess,senderId, receivedId, 
                new Protocol.ItemPutInBagParameter { ChangedItemId =  changedItemId });
    }

}