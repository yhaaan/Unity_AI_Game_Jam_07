#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("ProjectRTD.Gameplay.EditModeTests")]

namespace ProjectRTD.Gameplay
{
    internal static class AnimalPartCsv
    {
        private const string Columns = "partId,animal,slot,displayName,description,weightDelta,healthDelta,heightDelta,skill";

        internal static List<PartSheetRow> Parse(string csv)
        {
            var headers = new Dictionary<string, int>(StringComparer.Ordinal);
            var parts = new List<PartSheetRow>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pairs = new HashSet<(PartAnimal, PartSlot)>();
            int row = 0, width = 0;
            csv = csv.TrimStart('\uFEFF');
            char delimiter = csv.TrimStart().StartsWith("partId\t", StringComparison.Ordinal) ? '\t' : ',';
            foreach (var cells in Records(csv, delimiter))
            {
                row++;
                if (cells.Length == 1 && string.IsNullOrWhiteSpace(cells[0])) continue;
                if (width == 0)
                {
                    width = cells.Length;
                    if (width != Columns.Split(',').Length) throw Error(row, "헤더", "필요한 열: " + Columns);
                    for (int i = 0; i < width; i++)
                        if (!headers.TryAdd(cells[i].Trim(), i)) throw Error(row, cells[i], "중복 헤더");
                    foreach (var key in Columns.Split(','))
                        if (!headers.ContainsKey(key)) throw Error(row, key, "필수 헤더 누락");
                    continue;
                }
                if (cells.Length != width) throw Error(row, "CSV", $"열 수 {cells.Length} (예상 {width})");
                string Get(string key) => cells[headers[key]].Trim();
                int Number(string key) => int.TryParse(Get(key), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int n) ? n : throw Error(row, key, "부호 있는 정수 필요");
                T Named<T>(string key, string value) where T : struct, Enum => Enum.GetNames(typeof(T)).Contains(value, StringComparer.OrdinalIgnoreCase) ? (T)Enum.Parse(typeof(T), value, true) : throw Error(row, key, "미등록 값: " + value);
                var p = new PartSheetRow { partId = Get("partId"), displayName = Get("displayName"), description = Get("description"),
                    animal = Named<PartAnimal>("animal", Get("animal").Equals("upa", StringComparison.OrdinalIgnoreCase) ? "Axolotl" : Get("animal")), slot = Named<PartSlot>("slot", Get("slot")),
                    heightDelta = Get("heightDelta") == "-" ? 0 : Number("heightDelta"), weightDelta = Number("weightDelta"), healthDelta = Number("healthDelta") };
                if (!Regex.IsMatch(p.partId, @"\A[A-Za-z0-9_-]+\z") || !ids.Add(p.partId)) throw Error(row, "partId", "빈 키, 잘못된 문자 또는 중복: " + p.partId);
                if (p.displayName.Length == 0) throw Error(row, "displayName", "빈 표시명");
                if (!pairs.Add((p.animal, p.slot))) throw Error(row, "animal/slot", "중복 조합: " + p.animal + "/" + p.slot);
                if (Get("skill").Length > 0) p.effectIds = Get("skill").Split(',').Select(s => s.Trim().ToLowerInvariant() switch
                {
                    "canfly" => PartEffect.Glide,
                    "canpass" => PartEffect.CanPass,
                    "canswim" => PartEffect.Float,
                    _ => throw Error(row, "skill", "미등록 스킬: " + s.Trim())
                }).ToArray();
                parts.Add(p);
            }
            foreach (PartAnimal animal in Enum.GetValues(typeof(PartAnimal)))
                foreach (PartSlot slot in Enum.GetValues(typeof(PartSlot)))
                    if (!pairs.Contains((animal, slot))) throw new FormatException($"animal/slot 조합 누락: {animal}/{slot}");
            return parts;
        }

        private static FormatException Error(int row, string column, string message) => new FormatException($"{row}행 [{column}]: {message}");

        private static IEnumerable<string[]> Records(string text, char delimiter)
        {
            var cells = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false, closed = false;
            int record = 1;
            for (int i = 0; i <= text.Length; i++)
            {
                bool end = i == text.Length;
                char c = end ? '\0' : text[i];
                if (quoted)
                {
                    if (end) throw Error(record, (cells.Count + 1).ToString(), "닫히지 않은 따옴표");
                    if (c != '"') cell.Append(c);
                    else if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else { quoted = false; closed = true; }
                }
                else if (end || c == delimiter || c == '\r' || c == '\n')
                {
                    cells.Add(cell.ToString()); cell.Clear(); closed = false;
                    if (c == delimiter) continue;
                    yield return cells.ToArray(); cells.Clear(); record++;
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                }
                else if (!closed && c == '"' && cell.Length == 0) quoted = true;
                else if (closed || c == '"') throw Error(record, (cells.Count + 1).ToString(), "잘못된 따옴표 구분");
                else cell.Append(c);
            }
        }
    }
}
#endif
