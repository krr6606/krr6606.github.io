using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

namespace TeamSH
{
    /// <summary>
    /// 자신의 2차원 범위 안에 들어와 있는지 확인하는 클래스 
    /// </summary>
    public class GridCell
    {
        private float halfWidth;
        private float halfHeight;
        private Vector3 centerPosition;
        private float boundaryMinX;
        private float boundaryMinZ;
        private float boundaryMaxX;
        private float boundaryMaxZ;
        public Bounds boxBounds;

        public GridCell(float width, float height, Vector3 centerPosition)
        {
            this.halfWidth = width / 2f;
            this.halfHeight = height / 2f;
            this.centerPosition = centerPosition;
            this.boundaryMinX = centerPosition.x - halfWidth;
            this.boundaryMaxX = centerPosition.x + halfWidth;
            this.boundaryMinZ = centerPosition.z - halfHeight;
            this.boundaryMaxZ = centerPosition.z + halfHeight;
            boxBounds = new Bounds(centerPosition,
                new Vector3(width, float.MaxValue, height)); // 2d와 다름 없음으로 높이는 무한으로 설정
        }

        /// <summary>
        /// 2차원 범위 안에 들어와 있는지 확인하는 메서드
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public bool IsStay(Vector3 target)
        {
            if (halfHeight == 0 && halfWidth == 0)
            {
                Debug.LogWarning("그리드의 크기가 0입니다. 그리드 위치: " + centerPosition);
                return false;
            }

            //Debug.Log($"그리드 크기 X 여부{target.x >= boundaryMinX && target.x <= boundaryMaxX}, Z 여부 {target.z >= boundaryMinZ && target.z <= boundaryMaxZ}");
            if (target.x >= boundaryMinX && target.x <= boundaryMaxX &&
                target.z >= boundaryMinZ && target.z <= boundaryMaxZ)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 두 점 사이에 이 그리드가 있는지 확인하는 메서드
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public bool RayCheck(Vector3 origin, Vector3 target)
        {
            Vector3 direction = target - origin;
            float distance = direction.magnitude;
            // 무한한 거리의 레이를 생성
            Ray ray = new Ray(origin, direction.normalized);

            // 박스와 레이가 만나고, 그 교차 거리가 선분 길이(distance) 이내인지 확인
            if (boxBounds.IntersectRay(ray, out float hitDistance) && hitDistance <= distance)
            {
                // 선분이 박스와 교차함
                return true;
            }

            return false;
        }

        public Vector3 GetGridCorner(int i)
        {
            if (i == 0)
            {
                return new Vector3(boundaryMinX, centerPosition.y, boundaryMaxZ);
            }
            else if (i == 1)
            {
                return new Vector3(boundaryMaxX, centerPosition.y, boundaryMaxZ);
            }
            else if (i == 2)
            {
                return new Vector3(boundaryMaxX, centerPosition.y, boundaryMinZ);
            }
            else if (i == 3)
            {
                return new Vector3(boundaryMinX, centerPosition.y, boundaryMinZ);
            }
            else
            {
                Debug.LogWarning("GetGridCorner: Invalid index " + i);
                return Vector3.zero;
            }
        }

        public Vector3 GetCenterPosition()
        {
            return centerPosition;
        }
    }
}
