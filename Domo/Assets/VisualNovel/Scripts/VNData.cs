using System;
using System.Collections.Generic;
using UnityEngine;

namespace VisualNovel
{
    /// <summary>
    /// 스크립트 전체. JSON(외부) 또는 인스펙터(내부)에서 채운다.
    /// </summary>
    [Serializable]
    public class VNScript
    {
        [Tooltip("대사 안의 {키} 를 값으로 치환합니다. 예: {player} → 모아")]
        public List<VNVariable> variables = new List<VNVariable>();

        [Tooltip("자주 쓰는 캐릭터를 미리 등록해 두면 대사에서 id 만으로 호출할 수 있습니다.")]
        public List<VNCharacterDef> characters = new List<VNCharacterDef>();

        public List<VNLine> lines = new List<VNLine>();
    }

    [Serializable]
    public class VNVariable
    {
        public string key;
        public string value;
    }

    [Serializable]
    public class VNCharacterDef
    {
        [Tooltip("대사에서 참조할 고유 id")]
        public string id;
        [Tooltip("이름표에 표시될 닉네임")]
        public string nickname;
        [Tooltip("이미지 경로 (StreamingAssets/VN 기준 상대경로, 절대경로, http URL, Resources 경로)")]
        public string image;
        [Tooltip("인스펙터에서 직접 지정하는 이미지 (image 보다 우선)")]
        public Sprite sprite;
        [Tooltip("이름표 글자색 (#RRGGBB)")]
        public string nameColor = "#FFFFFF";

        [Tooltip("true 면 초상화(잘라서 크게), false 면 전신")]
        public bool portrait;
        [Tooltip("초상화 자르기 비율 (가로/세로). 4:3 = 1.333")]
        public float portraitAspect = 4f / 3f;
        [Tooltip("초상화로 자를 세로 위치 (0 = 맨 위, 1 = 맨 아래)")]
        [Range(0f, 1f)] public float portraitFocus;
    }

    [Serializable]
    public class VNLine
    {
        [Tooltip("배경 이미지. 비우면 이전 배경 유지, \"none\" 이면 배경 제거")]
        public string background;
        public Sprite backgroundSprite;

        [Tooltip("등록된 캐릭터 id (선택)")]
        public string character;

        [Tooltip("이름표 닉네임. 비우면 캐릭터의 닉네임 사용, \"none\" 이면 이름표 숨김")]
        public string nickname;

        [Tooltip("인물 이미지. 비우면 캐릭터 기본 이미지/이전 이미지 유지, \"none\" 이면 해당 위치 인물 제거")]
        public string image;
        public Sprite sprite;

        [Tooltip("left / center / right")]
        public string position = "center";

        [TextArea(2, 6)]
        public string text;

        [Tooltip("이 대사 전에 모든 인물을 지움")]
        public bool clearCharacters;

        [Tooltip("이 대사 전에 지정한 위치의 인물만 지움 (예: \"left,right\")")]
        public string clearSlots;
    }
}
