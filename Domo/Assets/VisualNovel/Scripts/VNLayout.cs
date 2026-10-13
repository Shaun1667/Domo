using UnityEngine;

namespace VisualNovel
{
    /// <summary>
    /// 인물 배치 계산. 게임 화면(VNPlayer)과 설정 화면 미리보기(VNLauncher)가 같은 값을 쓰도록 한 곳에 둔다.
    /// 좌표는 화면(또는 미리보기 틀) 기준 0~1 비율.
    /// </summary>
    public static class VNLayout
    {
        /// <summary>왼쪽 / 가운데 / 오른쪽 인물의 가로 중심</summary>
        public static readonly float[] SlotX = { 0.2f, 0.5f, 0.8f };

        /// <summary>크기 1 일 때 화면 높이 대비 인물 높이</summary>
        public const float FullBodyHeight = 0.92f;
        public const float PortraitHeight = 0.8f;

        /// <summary>대화창 위치 (1920×1080 기준 좌우 60, 아래 30, 높이 300)</summary>
        public static readonly Vector2 DialogueBoxMin = new Vector2(60f / 1920f, 30f / 1080f);
        public static readonly Vector2 DialogueBoxMax = new Vector2(1f - 60f / 1920f, 330f / 1080f);

        /// <summary>
        /// 인물 이미지 영역 배치. 이미지 아래 끝이 offsetY(0 = 화면 바닥) 에 오고,
        /// 높이는 기본 높이 × scale. 가로 폭은 AspectRatioFitter 가 이미지 비율로 맞춘다.
        /// </summary>
        public static void ApplySlot(RectTransform rt, int slot, bool portrait, float scale, float offsetY)
        {
            float h = (portrait ? PortraitHeight : FullBodyHeight) * Mathf.Clamp(scale <= 0f ? 1f : scale, 0.1f, 3f);
            float x = SlotX[Mathf.Clamp(slot, 0, SlotX.Length - 1)];
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchorMin = new Vector2(x, offsetY);
            rt.anchorMax = new Vector2(x, offsetY + h);
            rt.offsetMin = new Vector2(rt.offsetMin.x, 0f);
            rt.offsetMax = new Vector2(rt.offsetMax.x, 0f);
            rt.anchoredPosition = new Vector2(0f, 0f);
        }
    }
}
