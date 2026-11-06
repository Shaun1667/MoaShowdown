using System.Linq;
using Moa.Showdown.Game;
using Moa.Showdown.Game.Battle;
using Moa.Showdown.Game.Data;
using Moa.Showdown.Game.Shop;
using Moa.Showdown.Game.Ui;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Moa.Showdown.Editor.Setup
{
    /// <summary>
    /// 전투 씬 (Assets/_Project/Scenes/Battle.unity). 몇 번 실행해도 같은 결과:
    /// 없는 오브젝트만 만들고, 비어 있는 참조만 채운다 (인스펙터에서 고친 값 · 카메라 위치 보존). 빌드 씬 목록 맨 앞에 넣는다.
    /// </summary>
    public static class BattleSceneSetup
    {
        public const string ScenePath = "Assets/_Project/Scenes/Battle.unity";

        [MenuItem("MoaShowdown/Setup/Battle Scene")]
        public static void SetupBattleScene()
        {
            BalanceSetup.CreateBattleBalance();
            DebugTeamsSetup.CreateDebugTeams();
            SpeciesSetup.CreateSpeciesTable();
            SpeciesSetup.FillSpeciesAbilities();
            LocalOpponentsSetup.CreateLocalOpponents();
            RunSetup.CreateRunBalance();

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = System.IO.File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Camera cam = FindOrCreate("Main Camera", go =>
            {
                go.tag = "MainCamera";
                var c = go.AddComponent<Camera>();
                c.clearFlags = CameraClearFlags.SolidColor;
                c.backgroundColor = new Color(0.08f, 0.09f, 0.11f);
                c.fieldOfView = 40f;
                go.transform.position = new Vector3(0f, 6.5f, -10f);
                go.transform.LookAt(new Vector3(0f, -0.3f, 0.2f));
            }).GetComponent<Camera>();

            FindOrCreate("Directional Light", go =>
            {
                var l = go.AddComponent<Light>();
                l.type = LightType.Directional;
                l.intensity = 1.2f;
                go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            });

            GameObject rootGo = FindOrCreate("GameRoot", go => go.AddComponent<GameRoot>());
            BattleStage stage = GetOrAdd<BattleStage>(FindOrCreateChild(rootGo, "Stage"));
            BattleHud hud = GetOrAdd<BattleHud>(FindOrCreateChild(rootGo, "Hud"));
            ShopView shopView = GetOrAdd<ShopView>(FindOrCreateChild(rootGo, "Shop"));
            TooltipView tooltip = GetOrAdd<TooltipView>(FindOrCreateChild(rootGo, "Tooltip"));
            CarouselView carouselView = GetOrAdd<CarouselView>(FindOrCreateChild(rootGo, "Carousel"));

            var so = new SerializedObject(rootGo.GetComponent<GameRoot>());
            SetIfNull(so, "balance", AssetDatabase.LoadAssetAtPath<BattleBalance>(BalanceSetup.BattleBalancePath));
            SetIfNull(so, "debugTeams", AssetDatabase.LoadAssetAtPath<DebugTeamSet>(DebugTeamsSetup.DebugTeamsPath));
            SetIfNull(so, "speciesTable", AssetDatabase.LoadAssetAtPath<SpeciesTable>(SpeciesSetup.SpeciesTablePath));
            SetIfNull(so, "runBalance", AssetDatabase.LoadAssetAtPath<RunBalance>(RunSetup.RunBalancePath));
            SetIfNull(so, "mainCamera", cam);
            SetIfNull(so, "stage", stage);
            SetIfNull(so, "hud", hud);
            SetIfNull(so, "shopView", shopView);
            SetIfNull(so, "tooltip", tooltip);
            SetIfNull(so, "carouselView", carouselView);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log($"[Setup] {ScenePath} 준비 완료");
        }

        private static GameObject FindOrCreate(string name, System.Action<GameObject> init)
        {
            var go = GameObject.Find(name);
            if (go != null) return go;
            go = new GameObject(name);
            init(go);
            return go;
        }

        private static GameObject FindOrCreateChild(GameObject parent, string name)
        {
            Transform t = parent.transform.Find(name);
            if (t != null) return t.gameObject;
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        private static void SetIfNull(SerializedObject so, string field, Object value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p.objectReferenceValue == null) p.objectReferenceValue = value;
        }
    }
}
