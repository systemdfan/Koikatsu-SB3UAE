using System.Text;
using System.Reflection;
using UnityPlugin;
using Models;

namespace Processing
{
    public class DialogueExtractor
    {
        public List<DialogueLine> ExtractDialogueLines(MonoBehaviour monobeh)
        {
            var result = new List<DialogueLine>();
            if (monobeh?.Parser?.type?.Members == null)
                return result;

            int topIndex = 0;
            foreach (var member in monobeh.Parser.type.Members)
            {
                TraverseUType(member, result, monobeh, $"Member[{topIndex}]");
                topIndex++;
            }

            return result;
        }

        private void TraverseUType(UType utype, List<DialogueLine> result, MonoBehaviour monobeh, string path)
        {
            if (utype == null) return;

            if (utype is Uarray uarr)
            {
                if (uarr.Value?.Length == 2)
                {
                    string rawFirst = ExtractFirstString(uarr.Value[0]);
                    string rawSecond = ExtractFirstString(uarr.Value[1]);

                    string first = NormalizeString(rawFirst);
                    string second = NormalizeString(rawSecond);

                    Console.WriteLine($"[DBG] PAIR_CANDIDATE Mono:{monobeh?.m_Name} path={path} -> firstRaw='{rawFirst}' firstNorm='{first}' secondRaw='{rawSecond}' secondNorm='{second}'");

                    if (IsPlaceHolder(first) && !string.IsNullOrWhiteSpace(second))
                    {
                        Console.WriteLine($"[DBG] ADDED_PAIR Mono:{monobeh?.m_Name} path={path} -> '{first}':'{second}'");
                        result.Add(new DialogueLine(first, second));
                        
                        return;
                    }
                }

                if (uarr.Value != null)
                {
                    for (int i = 0; i < uarr.Value.Length; i++)
                    {
                        TraverseUType(uarr.Value[i], result, monobeh, path + $"[{i}]");
                    }
                }
                return;
            }

            if (utype is UClass uclass)
            {
                if (uclass.Members != null)
                {
                    for (int i = 0; i < uclass.Members.Count; i++)
                    {
                        var child = uclass.Members[i];
                        string memberName = TryGetMemberName(uclass, i) ?? $"Member[{i}]";
                        TraverseUType(child, result, monobeh, path + "." + memberName);
                    }
                }
                return;
            }
        }
        private string ExtractFirstString(UType utype)
        {
            if (utype == null) return string.Empty;

            if (utype is Uarray uarr && IsByteArray(uarr))
            {
                string s = TryDecodeByteArray(uarr);
                if (!string.IsNullOrEmpty(s)) return s;
            }

            if (utype is Uarray uarr2)
            {
                if (uarr2.Value != null)
                {
                    foreach (var el in uarr2.Value)
                    {
                        string found = ExtractFirstString(el);
                        if (!string.IsNullOrEmpty(found)) return found;
                    }
                }
                return string.Empty;
            }

            if (utype is UClass uclass)
            {
                if (uclass.Members != null)
                {
                    for (int i = 0; i < uclass.Members.Count; i++)
                    {
                        string found = ExtractFirstString(uclass.Members[i]);
                        if (!string.IsNullOrEmpty(found)) return found;
                    }
                }
                return string.Empty;
            }

            var raw = GetRawValue(utype);
            if (raw == null) return string.Empty;

            if (raw is byte[] ba)
            {
                try { return Encoding.UTF8.GetString(ba).TrimEnd('\0'); }
                catch { return Encoding.ASCII.GetString(ba).TrimEnd('\0'); }
            }

            return raw.ToString();
        }

        private bool IsPlaceHolder(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = NormalizeString(s);
            return s == "[H名]" || s == "[P名]";
        }

        private string TryDecodeByteArray(Uarray arr)
        {
            if (!IsByteArray(arr)) return string.Empty;
            var bytes = new List<byte>();
            foreach (var el in arr.Value)
            {
                if (el is Uchar uchar)
                {
                    var raw = GetRawValue(uchar);
                    if (raw is byte b) bytes.Add(b);
                    else if (raw is int ix && ix >= 0 && ix <= 255) bytes.Add((byte)ix);
                }
            }
            if (bytes.Count == 0) return string.Empty;
            try { return Encoding.UTF8.GetString(bytes.ToArray()).TrimEnd('\0'); }
            catch { return Encoding.ASCII.GetString(bytes.ToArray()).TrimEnd('\0'); }
        }

        private bool IsByteArray(Uarray arr)
        {
            if (arr?.Value == null) return false;
            return arr.Value.Length > 0 && arr.Value[0] is Uchar;
        }

        private string NormalizeString(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            s = s.Replace("\uFEFF", "").Replace("\u200B", "").Trim();
            try { s = s.Normalize(NormalizationForm.FormC); } catch { }
            return s;
        }

        private string TryGetMemberName(UClass uclass, int index)
        {
            try
            {
                var typeField = uclass.GetType().GetField("type", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var typ = typeField?.GetValue(uclass);
                if (typ != null)
                {
                    var membersProp = typ.GetType().GetProperty("Members", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var members = membersProp?.GetValue(typ) as System.Collections.IList;
                    if (members != null && index < members.Count)
                    {
                        var memberObj = members[index];
                        var nameProp = memberObj.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (nameProp != null)
                        {
                            var nm = nameProp.GetValue(memberObj) as string;
                            if (!string.IsNullOrEmpty(nm)) return nm;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private object GetRawValue(UType utype)
        {
            if (utype == null) return null;
            var t = utype.GetType();
            var f = t.GetField("Value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null) return f.GetValue(utype);
            var p = t.GetProperty("Value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null) return p.GetValue(utype);
            return null;
        }
    }
}
