using System.Text;
using System.Reflection;
using UnityPlugin;
using Models;
using logging;

namespace Processing
{
    public class DialogueExtractor
    {
        public List<DialogueLine> ExtractDialogueLines(MonoBehaviour monobeh)
        {
            var result = new List<DialogueLine>();
            if (monobeh?.Parser?.type?.Members == null)
                return result;

            Console.WriteLine($"\n=== Searching dialogues in MonoBehaviour: {monobeh.m_Name} ===");

            int topIndex = 0;
            foreach (var member in monobeh.Parser.type.Members)
            {
                SearchForCommand16(member, result, monobeh, $"Member[{topIndex}]");
                topIndex++;
            }

            Console.WriteLine($"=== FOUND {result.Count} dialogues ===");
            return result;
        }

        private void SearchForCommand16(UType utype, List<DialogueLine> result, MonoBehaviour monobeh, string path)
        {
            if (utype == null) return;

            if (utype is UClass uclass)
            {
                log.DebugLog($"[DEBUG] Chicking UClass in {path}, ClassName: {GetClassName(uclass)}, Members: {uclass.Members?.Count ?? 0}");

                // Проверяем, является ли этот UClass элементом с _command
                if (HasCommand16(uclass))
                {
                    log.DebugLog($"[FOUND] _command = 16 in {path}");

                    // Ищем _args внутри этого же UClass
                    var dialogueStrings = ExtractArgsFromCommand16(uclass);
                    if (dialogueStrings.Count >= 2) // Изменить условие с == на >=
                    {
                        string tag = NormalizeString(dialogueStrings[0]);

                        // Добавить проверку флага и выбор нужной строки
                        var languageMap = new Dictionary<string, (int index, string name)>
                        {
                            ["en-US"] = (2, "english"),
                            ["cn-TW"] = (3, "chinese traditional"),
                            ["cn-CN"] = (4, "chinese simplified")
                        };

                        string text;
                        if (Program.IsExtractinglanguage && languageMap.ContainsKey(Program.Language ?? ""))
                        {
                            var (index, name) = languageMap[Program.Language];
                            if (dialogueStrings.Count > index)
                            {
                                text = NormalizeString(dialogueStrings[index]);
                                log.DebugLog($"[DEBUG] Extracting {name}");
                            }
                            else
                            {
                                text = NormalizeString(dialogueStrings[1]);
                                log.DebugLog($"[DEBUG] i8n strings not found for {name}, unsing jp instead");
                            }
                        }
                        else
                        {
                            text = NormalizeString(dialogueStrings[1]);
                        }
                        result.Add(new DialogueLine(tag, text));
                    }
                    else if (dialogueStrings.Count > 0)
                    {
                        Console.WriteLine($"[WARNING] Found {dialogueStrings.Count} strings, 2 or more awaited");
                    }

                    return; // Нашли команду 16, больше в этой ветке искать не нужно
                }

                // Рекурсивно проверяем члены класса
                if (uclass.Members != null)
                {
                    for (int i = 0; i < uclass.Members.Count; i++)
                    {
                        string memberName = TryGetMemberName(uclass, i) ?? $"Member[{i}]";
                        SearchForCommand16(uclass.Members[i], result, monobeh, path + "." + memberName);
                    }
                }
            }
            else if (utype is Uarray uarray)
            {
                log.DebugLog($"[DEBUG] Checking Uarray in {path}, Length: {uarray.Value?.Length ?? 0}");

                // Рекурсивно проверяем элементы массива
                if (uarray.Value != null)
                {
                    for (int i = 0; i < uarray.Value.Length; i++)
                    {
                        SearchForCommand16(uarray.Value[i], result, monobeh, path + $"[{i}]");
                    }
                }
            }
            else
            {
                log.DebugLog($"[DEBUG] Skipping {utype?.GetType().Name} in {path}");
            }
        }

        /// <summary>
        /// Проверяет, содержит ли UClass поле _command со значением 16
        /// </summary>
        private bool HasCommand16(UClass uclass)
        {
            if (uclass?.Members == null || uclass.Members.Count < 5) return false;

            // Структура Param всегда:
            // [0] _hash (Uint32), [1] _version (Uint32), [2] _multi (Uint8), [3] _command (Uint32), [4] _args (UClass)
            // Проверим, что это именно такая структура
            if (GetClassName(uclass) == "Param" && uclass.Members.Count == 5)
            {
                var commandMember = uclass.Members[3]; // _command всегда на позиции 3
                log.DebugLog($"[DEBUG] Checking member[3] as _command, type: {commandMember?.GetType().Name}");

                var commandValue = GetRawValue(commandMember);
                log.DebugLog($"[DEBUG] Value of member[3]: {commandValue} (type: {commandValue?.GetType().Name})");

                if (commandValue is int intVal)
                {
                    log.DebugLog($"[DEBUG] _command = {intVal}, searching for 16");
                    if (intVal == 16)
                    {
                        log.DebugLog($"[DEBUG] ✓ FOUND _command = 16!");
                        return true;
                    }
                }
            }

            // Оставляем старый способ как резервный
            for (int i = 0; i < uclass.Members.Count; i++)
            {
                var member = uclass.Members[i];
                string memberName = TryGetMemberName(uclass, i);
                log.DebugLog($"[DEBUG] Checking the member {i}: name='{memberName}', type={member?.GetType().Name}");

                if (memberName == "_command")
                {
                    var commandValue = GetRawValue(member);
                    log.DebugLog($"[DEBUG] _command found using the name! Value: {commandValue}");

                    if (commandValue is int intVal && intVal == 16)
                    {
                        log.DebugLog($"[DEBUG] ✓ Found _command = 16 Using the name!");
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Извлекает строки из _args для команды 16
        /// </summary>
        private List<string> ExtractArgsFromCommand16(UClass commandClass)
        {
            var result = new List<string>();

            if (commandClass?.Members == null) return result;

            // Для Param структуры _args всегда на позиции 4
            if (GetClassName(commandClass) == "Param" && commandClass.Members.Count == 5)
            {
                var argsMember = commandClass.Members[4]; // _args на позиции 4
                log.DebugLog($"[DEBUG] _args member[4] type: {argsMember?.GetType().Name}");

                if (argsMember is UClass argsClass)
                {
                    string className = GetClassName(argsClass);
                    log.DebugLog($"[DEBUG] _args ClassName: {className}");

                    if (argsClass.Members != null && argsClass.Members.Count > 0)
                    {
                        // Ищем массив внутри vector (обычно в member[0])
                        var vectorMember = argsClass.Members[0];
                        if (vectorMember is Uarray argsArray)
                        {
                            log.DebugLog($"[DEBUG] Found array of length: {argsArray.Value?.Length ?? 0}");

                            // Извлекаем строки из массива
                            if (argsArray.Value != null)
                            {
                                foreach (var element in argsArray.Value)
                                {
                                    string extractedString = ExtractStringFromElement(element);
                                    result.Add(extractedString);
                                    if (!string.IsNullOrEmpty(extractedString))
                                    {
                                        log.DebugLog($"[DEBUG] Extracted string: '{extractedString}'");
                                    }
                                    else {
                                        log.DebugLog($"[DEBUG] Extracted the narrative sting");
                                    }
                                }
                            }
                        }
                    }
                }
                return result;
            }

            // Старый способ через имена как резерв
            for (int i = 0; i < commandClass.Members.Count; i++)
            {
                string memberName = TryGetMemberName(commandClass, i);
                if (memberName == "_args")
                {
                    var argsMember = commandClass.Members[i];
                    log.DebugLog($"[DEBUG] Found _args using the name on the position {i}");

                    if (argsMember is UClass argsClass)
                    {
                        string className = GetClassName(argsClass);
                        log.DebugLog($"[DEBUG] _args ClassName: {className}");

                        if (argsClass.Members != null)
                        {
                            for (int j = 0; j < argsClass.Members.Count; j++)
                            {
                                var vectorMember = argsClass.Members[j];
                                if (vectorMember is Uarray argsArray)
                                {
                                    log.DebugLog($"[DEBUG] Found array of length: {argsArray.Value?.Length ?? 0}");

                                    if (argsArray.Value != null)
                                    {
                                        foreach (var element in argsArray.Value)
                                        {
                                            string extractedString = ExtractStringFromElement(element);
                                            if (!string.IsNullOrEmpty(extractedString))
                                            {
                                                result.Add(extractedString);
                                                log.DebugLog($"[DEBUG] Extracted string: '{extractedString}'");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    break;
                }
            }

            return result;
        }

        /// <summary>
        /// Извлекает строку из элемента (обычно UClass с ClassName="string")
        /// </summary>
        private string ExtractStringFromElement(UType element)
        {
            if (element == null) return string.Empty;

            if (element is UClass stringClass)
            {
                string className = GetClassName(stringClass);
                if (className == "string" && stringClass.Members != null)
                {
                    foreach (var member in stringClass.Members)
                    {
                        if (member is Uarray byteArray && IsByteArray(byteArray))
                        {
                            return TryDecodeByteArray(byteArray);
                        }
                        // Добавить проверку на пустой массив:
                        else if (member is Uarray emptyArray && emptyArray.Value?.Length == 0)
                        {
                            log.DebugLog("[DEBUG] Found empty array. Returning empty string");
                            return string.Empty; // Пустой тег для повествования
                        }
                    }
                }
            }

            if (element is Uarray directArray)
            {
                if (IsByteArray(directArray))
                {
                    return TryDecodeByteArray(directArray);
                }
                // Добавить проверку на пустой массив:
                else if (directArray.Value?.Length == 0)
                {
                    log.DebugLog("[DEBUG] Found straight empty array. Returning empty string");
                    return string.Empty;
                }
            }

            return string.Empty;
        }

        private string GetClassName(UClass uclass)
        {
            try
            {
                var classNameProp = uclass.GetType().GetProperty("ClassName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (classNameProp != null)
                {
                    var className = classNameProp.GetValue(uclass) as string;
                    if (!string.IsNullOrEmpty(className)) return className;
                }

                var classNameField = uclass.GetType().GetField("ClassName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (classNameField != null)
                {
                    var className = classNameField.GetValue(uclass) as string;
                    if (!string.IsNullOrEmpty(className)) return className;
                }
            }
            catch { }

            return "Unknown";
        }

        private void TraverseUType(UType utype, List<DialogueLine> result, MonoBehaviour monobeh, string path)
        {
            // заглушка
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