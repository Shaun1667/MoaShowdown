using System.Collections.Generic;
using System.IO;
using System.Linq;
using Moa.Showdown.Game.Battle;
using Moa.Showdown.Game.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Moa.Showdown.Editor.Setup
{
    /// <summary>
    /// 몬스터 3D 모델 연결 (Phase G). 여러 번 돌려도 같은 결과 (멱등).
    /// 1) `Art/&lt;도구&gt;/&lt;몬스터 ID&gt;.fbx`(또는 .obj · 프리팹)를 찾는다 — 파일 이름 = 몬스터 ID (예: Art/Varco/ember.fbx).
    /// 2) 모델마다 프리팹 `Art/Prefabs/Monsters/&lt;ID&gt;.prefab`을 만든다: 높이 = 표의 targetHeight, 바닥 가운데 = 원점, 충돌체 없음.
    /// 3) 표 `Data/MonsterVisuals.asset`에 몬스터마다 한 줄 (손으로 넣은 크기 · 방향 · 높이 보정은 그대로).
    /// 4) 열려 있는 씬의 BattleStage에 표가 비어 있으면 연결.
    /// </summary>
    public static class MonsterVisualSetup
    {
        public const string TablePath = "Assets/_Project/Data/MonsterVisuals.asset";
        public const string ArtRoot = "Assets/_Project/Art";
        public const string PrefabDir = "Assets/_Project/Art/Prefabs/Monsters";

        private static readonly string[] ModelExtensions = { ".fbx", ".obj", ".prefab" };

        [MenuItem("MoaShowdown/Setup/Monster Visuals")]
        public static void Run()
        {
            var species = AssetDatabase.LoadAssetAtPath<SpeciesTable>(SpeciesSetup.SpeciesTablePath);
            if (species == null)
            {
                Debug.LogError($"[Setup] {SpeciesSetup.SpeciesTablePath} 없음 — 먼저 Setup/Species Table");
                return;
            }

            MonsterVisualTable table = LoadOrCreateTable();
            List<string> ids = species.rows.Select(r => r.id).ToList();
            SyncRows(table, ids);

            Dictionary<string, string> models = FindModels(ids, out List<string> unmatched, out List<string> glb);
            int built = 0;
            foreach (MonsterVisualRow row in table.rows)
            {
                if (!models.TryGetValue(row.speciesId, out string modelPath)) continue;
                GameObject prefab = BuildPrefab(row.speciesId, modelPath, table.targetHeight);
                if (prefab == null) continue;
                row.prefab = prefab;
                built++;
            }

            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            WireStage(table);

            List<string> missing = table.rows.Where(r => r.prefab == null).Select(r => r.speciesId).ToList();
            Debug.Log($"[Setup] Monster Visuals: 몬스터 {table.rows.Count}종 · 모델 연결 {table.rows.Count - missing.Count}종 (이번에 프리팹 {built}개 만듦)"
                + (missing.Count > 0 ? $"\n  모델 없음(캡슐로 그림): {string.Join(", ", missing)}" : "")
                + (unmatched.Count > 0 ? $"\n  이름이 몬스터 ID와 안 맞는 파일(무시함): {string.Join(", ", unmatched)}" : "")
                + (glb.Count > 0 ? $"\n  GLB/GLTF는 아직 못 읽음 — FBX로 받아 주세요: {string.Join(", ", glb)}" : ""));
        }

        private static MonsterVisualTable LoadOrCreateTable()
        {
            var table = AssetDatabase.LoadAssetAtPath<MonsterVisualTable>(TablePath);
            if (table != null) return table;
            table = ScriptableObject.CreateInstance<MonsterVisualTable>();
            AssetDatabase.CreateAsset(table, TablePath);
            Debug.Log($"[Setup] {TablePath} 생성");
            return table;
        }

        /// <summary>몬스터 표 순서대로 한 줄씩 (없는 줄만 추가, 있는 줄의 보정 값은 그대로).</summary>
        private static void SyncRows(MonsterVisualTable table, List<string> ids)
        {
            var byId = table.rows.Where(r => r != null && !string.IsNullOrEmpty(r.speciesId)).GroupBy(r => r.speciesId).ToDictionary(g => g.Key, g => g.First());
            var ordered = new List<MonsterVisualRow>();
            foreach (string id in ids)
                ordered.Add(byId.TryGetValue(id, out MonsterVisualRow r) ? r : new MonsterVisualRow { speciesId = id });
            // 몬스터 표에 없는 줄(지운 몬스터)은 뒤에 남겨 둔다 — 보정 값을 잃지 않게
            ordered.AddRange(table.rows.Where(r => r != null && !ids.Contains(r.speciesId)));
            table.rows = ordered;
        }

        /// <summary>Art 아래(만든 프리팹 폴더 제외)에서 파일 이름 = 몬스터 ID인 모델. 같은 ID가 여럿이면 경로 순서로 첫 번째.</summary>
        private static Dictionary<string, string> FindModels(List<string> ids, out List<string> unmatched, out List<string> glb)
        {
            var result = new Dictionary<string, string>();
            unmatched = new List<string>();
            glb = new List<string>();
            string fullRoot = Path.GetFullPath(ArtRoot);
            if (!Directory.Exists(fullRoot)) return result;

            var files = Directory.GetFiles(fullRoot, "*.*", SearchOption.AllDirectories)
                .Select(f => "Assets" + f.Substring(Path.GetFullPath("Assets").Length).Replace('\\', '/'))
                .Where(p => !p.StartsWith(PrefabDir) && !p.Contains("_Textures/"))
                .OrderBy(p => p, System.StringComparer.Ordinal);
            foreach (string path in files)
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".glb" || ext == ".gltf")
                {
                    glb.Add(path);
                    continue;
                }
                if (!ModelExtensions.Contains(ext)) continue;
                string id = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                if (!ids.Contains(id))
                {
                    unmatched.Add(path);
                    continue;
                }
                if (!result.ContainsKey(id)) result[id] = path;
            }
            return result;
        }

        /// <summary>모델을 감싼 프리팹: 높이를 targetHeight로, 바닥 가운데를 원점으로. 렌더러가 없으면 null.</summary>
        private static GameObject BuildPrefab(string id, string modelPath, float targetHeight)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (source == null)
            {
                Debug.LogWarning($"[Setup] {modelPath}: 모델을 읽지 못함");
                return null;
            }

            Material textured = BuildTexturedMaterial(id, modelPath, source);

            var root = new GameObject(id);
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform) ?? Object.Instantiate(source, root.transform);
                model.transform.localPosition = Vector3.zero;
                foreach (Collider c in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                if (textured != null)
                    foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
                        r.sharedMaterials = Enumerable.Repeat(textured, r.sharedMaterials.Length).ToArray();

                if (!TryBounds(model, out Bounds b) || b.size.y <= 0.0001f)
                {
                    Debug.LogWarning($"[Setup] {modelPath}: 보이는 메시가 없음");
                    return null;
                }
                float scale = targetHeight / b.size.y;
                model.transform.localScale *= scale;
                TryBounds(model, out b);
                model.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);

                EnsureFolder(PrefabDir);
                string prefabPath = $"{PrefabDir}/{id}.prefab";
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// FBX 안의 텍스처를 쓰는 재질 (AI 도구 FBX는 텍스처를 품고 있지만 재질이 그걸 가리키지 않는 경우가 많다, Varco 확인 2026-11-09).
        /// 모델 재질에 이미 텍스처가 있으면 null(그대로 씀). 아니면 텍스처를 `&lt;ID&gt;_Textures/`로 꺼내고(한 번만),
        /// 이름에 base_color(또는 albedo · diffuse)가 든 그림 = 색, normal이 든 그림 = 요철로 URP Lit 재질 `Art/Prefabs/Monsters/&lt;ID&gt;.mat`을 만든다.
        /// </summary>
        private static Material BuildTexturedMaterial(string id, string modelPath, GameObject source)
        {
            if (source.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Any(m => m != null && m.mainTexture != null)) return null;

            string dir = $"{Path.GetDirectoryName(modelPath).Replace('\\', '/')}/{id}_Textures";
            if (AssetImporter.GetAtPath(modelPath) is ModelImporter importer && !AssetDatabase.IsValidFolder(dir))
            {
                EnsureFolder(dir);
                importer.ExtractTextures(dir);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            if (!AssetDatabase.IsValidFolder(dir)) return null;

            string[] textures = AssetDatabase.FindAssets("t:Texture2D", new[] { dir }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            string basePath = textures.FirstOrDefault(p => NameHas(p, "base_color", "basecolor", "albedo", "diffuse"));
            if (basePath == null) return null;
            string normalPath = textures.FirstOrDefault(p => NameHas(p, "normal"));
            if (normalPath != null && AssetImporter.GetAtPath(normalPath) is TextureImporter ni && ni.textureType != TextureImporterType.NormalMap)
            {
                ni.textureType = TextureImporterType.NormalMap;
                ni.SaveAndReimport();
            }

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return null;
            EnsureFolder(PrefabDir);
            string matPath = $"{PrefabDir}/{id}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(lit);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.shader = lit;
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(basePath));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.2f); // 카툰 톤: 덜 반짝이게
            if (normalPath != null)
            {
                mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
                mat.EnableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static bool NameHas(string path, params string[] words)
        {
            string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            return words.Any(w => name.Contains(w));
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static bool TryBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return false;
            bounds = renderers[0].bounds;
            foreach (Renderer r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
            return true;
        }

        /// <summary>열려 있는 씬의 BattleStage에 표가 비어 있으면 연결하고 씬 저장.</summary>
        private static void WireStage(MonsterVisualTable table)
        {
            var stage = Object.FindAnyObjectByType<BattleStage>();
            if (stage == null) return;
            var so = new SerializedObject(stage);
            SerializedProperty p = so.FindProperty("visuals");
            if (p == null || p.objectReferenceValue != null) return;
            p.objectReferenceValue = table;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
            EditorSceneManager.SaveScene(stage.gameObject.scene);
            Debug.Log("[Setup] BattleStage에 MonsterVisuals 연결");
        }
    }
}
