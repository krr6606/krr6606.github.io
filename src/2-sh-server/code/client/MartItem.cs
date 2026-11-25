using UnityEngine;
using UnityEngine.Serialization;
using DG.Tweening;

namespace TeamSH
{
    /// <summary>
    /// 임시로 지정해 놓은 아이템 클래스. 나중에 아이템의 속성이나 기능이 추가될 수 있음.
    /// </summary>
    public class MartItem : MonoBehaviour
    {
        private DisplayStand displayStand; // 집에서 ID기반으로 변경해서 DisplayStandManager를 통하여 RequestActiveItem이 일어나게 하도록 수정할 것
        private int itemIndex = -1; //매대 아이템 리스트 인덱스
        public ItemDef itemDef;

        [Header("애니메이션 설정")] 
        [SerializeField] private float originY = 0f;
        [SerializeField] private float moveDistance = 0.5f; // 위아래 이동 거리
        [SerializeField] private float moveDuration = 1.2f;   // 위아래 왕복 주기
        [SerializeField] private float rotateDuration = 5.0f; // 1회 회전 시간

        private Sequence idleSequence;

        private void Awake()
        {
            originY = transform.position.y;
        }
        
        public void SetItemInfo(DisplayStand displayStand, ItemDef itemDef, int index)
        {
            this.displayStand = displayStand;
            this.itemDef = itemDef;
            this.itemIndex = index;
        }

        public void RequestActiveItem()
        {
            displayStand.RequestActiveItem(itemIndex);
        }
        
        private void OnEnable()
        {
            StartIdleAnimation();
        }

        private void OnDisable()
        {
            KillAnimation();
        }

        #region 마트 아이템 애니메이션

        public void StartIdleAnimation()
        {
            // 기존 진행 중인 트윈이 있다면 정지
            KillAnimation();

            idleSequence = DOTween.Sequence();

            // 위아래 약한 왕복 운동
            Tween moveTween = transform.DOMoveY(originY + moveDistance, moveDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);

            // Y축 회전
            Tween rotateTween = transform.DORotate(new Vector3(0f, 360f, 0f), rotateDuration, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Incremental);
            
            idleSequence.Join(moveTween);
            idleSequence.Join(rotateTween);
        }
        
        // 회전 정지
        public void KillAnimation()
        {
            if (idleSequence != null && idleSequence.IsActive())
            {
                idleSequence.Kill();
                idleSequence = null;
            }
        }

        #endregion
        
    }
}