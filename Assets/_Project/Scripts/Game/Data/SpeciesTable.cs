using System;
using System.Collections.Generic;
using System.Linq;
using Moa.Showdown.Core.Content;
using UnityEngine;

namespace Moa.Showdown.Game.Data
{
    /// <summary>
    /// 몬스터 표 데이터 에셋 (BALANCE 7). 새 몬스터 = 줄 하나. ID는 스냅샷에 저장되므로 한번 정하면 바꾸지 않는다.
    /// 기본 줄은 Setup 메뉴(MoaShowdown/Setup/Species Table)가 만든다.
    /// </summary>
    [CreateAssetMenu(menuName = "MoaShowdown/Species Table", fileName = "SpeciesTable")]
    public sealed class SpeciesTable : ScriptableObject
    {
        public List<SpeciesRow> rows = new List<SpeciesRow>();

        public SpeciesCatalog ToCatalog() => new SpeciesCatalog(rows.Select(r => new SpeciesDef
        {
            Id = r.id,
            DisplayName = r.displayName,
            Attack = r.attack,
            AttackIntervalTicks = r.attackIntervalTicks,
            Rarity = r.rarity,
        }));
    }

    [Serializable]
    public sealed class SpeciesRow
    {
        public string id;
        public string displayName;
        public int attack;
        [Tooltip("공격 간격(틱). 10틱 = 1초")]
        public int attackIntervalTicks;
        [Tooltip("등급 = 상점 가격 (BALANCE 10)")]
        public Rarity rarity;
    }
}
