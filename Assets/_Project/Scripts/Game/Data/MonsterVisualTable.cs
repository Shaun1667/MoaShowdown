using System;
using System.Collections.Generic;
using UnityEngine;

namespace Moa.Showdown.Game.Data
{
    /// <summary>
    /// 몬스터 ID → 3D 모델 (Phase G, CLAUDE 핵심 규칙 8: 데이터는 비주얼을 ID로만 참조). 모델이 없으면 그레이박스(캡슐)로 그린다.
    /// 줄 · 프리팹은 Setup 메뉴(MoaShowdown/Setup/Monster Visuals)가 만든다 — `Art/&lt;도구&gt;/&lt;몬스터 ID&gt;.fbx`를 찾아 크기(높이) · 바닥을 맞춘 프리팹.
    /// 크기 · 방향 · 높이 보정은 줄마다 손으로 (Setup을 다시 돌려도 보정 값은 그대로).
    /// </summary>
    [CreateAssetMenu(menuName = "MoaShowdown/Monster Visual Table", fileName = "MonsterVisuals")]
    public sealed class MonsterVisualTable : ScriptableObject
    {
        [Tooltip("Setup이 프리팹을 만들 때 모델 높이를 이 값(월드 단위)에 맞춘다")]
        public float targetHeight = 0.9f;

        public List<MonsterVisualRow> rows = new List<MonsterVisualRow>();

        /// <summary>그 몬스터의 줄 (모델이 없거나 줄이 없으면 null).</summary>
        public MonsterVisualRow Find(string speciesId)
        {
            if (string.IsNullOrEmpty(speciesId)) return null;
            foreach (MonsterVisualRow r in rows)
                if (r.speciesId == speciesId) return r.prefab != null ? r : null;
            return null;
        }
    }

    [Serializable]
    public sealed class MonsterVisualRow
    {
        public string speciesId;
        [Tooltip("Setup이 만든 프리팹 (바닥 = 원점, 높이 = targetHeight). 비면 그레이박스")]
        public GameObject prefab;
        [Tooltip("크기 보정 (1 = 그대로)")]
        public float scale = 1f;
        [Tooltip("방향 보정 (도, 모델 앞쪽이 +Z가 아니면)")]
        public float yaw;
        [Tooltip("높이 보정 (월드 단위)")]
        public float yOffset;
    }
}
