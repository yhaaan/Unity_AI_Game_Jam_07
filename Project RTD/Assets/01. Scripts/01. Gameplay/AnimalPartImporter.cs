#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace ProjectRTD.Gameplay
{
    internal sealed class AnimalPartImporter : EditorWindow
    {
        private const string Data = "Assets/06. Data/";
        private const string CatalogPath = Data + "PartCatalog.asset";
        [SerializeField] private string link = "";
        private string message;
        private bool failed;
        private UnityWebRequest download;

        [MenuItem("Tools/RTD/Animal Parts Importer")]
        private static void Open() => GetWindow<AnimalPartImporter>("Animal Parts Importer");

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Parts 탭의 웹 게시 링크를 입력하세요. 전체 시트가 유효할 때만 파츠 및 카탈로그를 저장합니다.", MessageType.Info);
            link = EditorGUILayout.TextField("웹 게시 URL", link);
            using (new EditorGUI.DisabledScope(download != null || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("게시 시트 가져오기"))
                {
                    try
                    {
                        download = UnityWebRequest.Get(CsvUrl(link));
                        download.timeout = 30;
                        download.SendWebRequest();
                        failed = false; message = "다운로드 중…";
                    }
                    catch (Exception e) { ReleaseRequest(); Fail(e); }
                }
                if (GUILayout.Button("시트에서 복사한 표 가져오기 (헤더 포함)"))
                {
                    try { Import(EditorGUIUtility.systemCopyBuffer); } catch (Exception e) { Fail(e); }
                }
                if (GUILayout.Button("로컬 CSV / TSV 가져오기"))
                {
                    var path = EditorUtility.OpenFilePanelWithFilters("Parts CSV", "", new[] { "Parts CSV", "csv,tsv,txt" });
                    if (path.Length != 0)
                        try { Import(File.ReadAllText(path)); } catch (Exception e) { Fail(e); }
                }
            }
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, failed ? MessageType.Error : MessageType.Info);
        }

        private static string CsvUrl(string input)
        {
            if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var u) || u.Scheme != "https" || u.Host != "docs.google.com" ||
                !Regex.IsMatch(u.AbsolutePath, @"^/spreadsheets/d/(?:e/)?[\w-]+/pub(?:html)?/?$"))
                throw new FormatException("Google Sheets의 ‘웹에 게시’로 만든 pub 또는 pubhtml 링크가 필요합니다.");
            var gid = Regex.Match(u.Query + u.Fragment, @"[?&#]gid=(\d+)(?=[&#]|$)");
            return "https://docs.google.com" + Regex.Replace(u.AbsolutePath, @"/pub(?:html)?/?$", "/pub") + "?output=csv" + (gid.Success ? "&single=true&gid=" + gid.Groups[1].Value : "");
        }

        private void Update()
        {
            if (download == null || !download.isDone) return;
            try
            {
                if (download.result != UnityWebRequest.Result.Success) throw new IOException("시트 다운로드 실패: " + download.error);
                if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("편집 모드에서 다시 가져오세요.");
                Import(download.downloadHandler.text);
            }
            catch (Exception e) { Fail(e); }
            finally { ReleaseRequest(); Repaint(); }
        }

        private void Fail(Exception e) { failed = true; message = e.Message; }
        private void OnDisable() => ReleaseRequest();
        private void ReleaseRequest() { download?.Dispose(); download = null; }

        private void Import(string csv)
        {
            message = ImportSheet(csv);
            failed = false;
        }

        // Upgrade the shipped five-animal catalog once. Subsequent sheet changes are explicit imports.
        [InitializeOnLoadMethod]
        private static void UpgradeCatalog() => EditorApplication.delayCall += () =>
        {
            var legacy = AssetDatabase.LoadAssetAtPath<AnimalPartDefinition>(Data + "Part_axolotl_head.asset");
            if (!EditorApplication.isPlayingOrWillChangePlaymode && legacy && legacy.PartId == "axolotl_head" && !File.Exists(Data + "Part_chicken_head.asset"))
                Debug.Log(ImportSheet(File.ReadAllText("Assets/99. Documents/PartsTemplate.txt")));
        };

        private static string ImportSheet(string csv)
        {
            var rows = AnimalPartCsv.Parse(csv);
            var existing = new Dictionary<string, AnimalPartDefinition>(StringComparer.Ordinal);
            foreach (var guid in AssetDatabase.FindAssets("t:AnimalPartDefinition", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var part = AssetDatabase.LoadAssetAtPath<AnimalPartDefinition>(path);
                if (!part || string.IsNullOrWhiteSpace(part.PartId) || !existing.TryAdd(part.PartId, part))
                    throw new InvalidOperationException("기존 에셋의 partId 누락 또는 중복: " + path);
            }
            // Change the sheet key in place, not the asset/GUID used by prefabs and art.
            foreach (var row in rows.Where(r => r.animal == PartAnimal.Axolotl))
            {
                string suffix = row.slot.ToString().ToLowerInvariant();
                string legacy = "axolotl_" + suffix;
                if (row.partId == "upa_" + suffix && !existing.ContainsKey(row.partId) && existing.TryGetValue(legacy, out var part) && part.Animal == row.animal && part.Slot == row.slot)
                {
                    existing.Remove(legacy);
                    existing.Add(row.partId, part);
                }
            }
            var missing = existing.Keys.Except(rows.Select(r => r.partId)).ToArray();
            if (missing.Length > 0) throw new InvalidOperationException("시트에서 사라진 기존 ID (변경 전 확인 필요): " + string.Join(", ", missing));
            string NewPath(string id) => Data + "Part_" + id + ".asset";
            foreach (var row in rows)
                if (!existing.ContainsKey(row.partId)) RequireFreePath(NewPath(row.partId));
            var catalog = AssetDatabase.LoadAssetAtPath<PartCatalog>(CatalogPath);
            if (!catalog) RequireFreePath(CatalogPath);

            var library = AssetDatabase.LoadAssetAtPath<ChimeraLayerLibrary>("Assets/04. Animations/PlayerLayers.yaml");
            var imported = new List<AnimalPartDefinition>();
            foreach (var row in rows)
            {
                bool isNew = !existing.TryGetValue(row.partId, out var part);
                if (isNew) part = CreateInstance<AnimalPartDefinition>();
                part.ApplySheet(row);
                if (isNew && library) part.ApplyArt(library.Find(part));
                if (isNew) AssetDatabase.CreateAsset(part, NewPath(row.partId));
                else EditorUtility.SetDirty(part);
                imported.Add(part);
            }
            bool newCatalog = !catalog;
            if (newCatalog) catalog = CreateInstance<PartCatalog>();
            catalog.SetParts(imported.ToArray());
            if (newCatalog) AssetDatabase.CreateAsset(catalog, CatalogPath);
            else EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return $"신규 {rows.Count - existing.Count}개, 갱신 {existing.Count}개. 카탈로그: {CatalogPath}";
        }

        private static void RequireFreePath(string path)
        {
            if (File.Exists(path) || Directory.Exists(path) || AssetDatabase.LoadMainAssetAtPath(path))
                throw new InvalidOperationException("다른 에셋이 경로를 사용 중입니다: " + path);
        }
    }
}
#endif
