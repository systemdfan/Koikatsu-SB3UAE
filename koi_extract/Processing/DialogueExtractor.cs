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

            Console.WriteLine($"\n=== ПОИСК ДИАЛОГОВ в MonoBehaviour: {monobeh.m_Name} ===");

            int topIndex = 0;
            foreach (var member in monobeh.Parser.type.Members)
            {
                SearchForCommand16(member, result, monobeh, $"Member[{topIndex}]");
                topIndex++;
            }

            Console.WriteLine($"=== НАЙДЕНО {result.Count} диалогов ===");
            return result;
        }

        private void SearchForCommand16(UType utype, List<DialogueLine> result, MonoBehaviour monobeh, string path)
        {
            if (utype == null) return;

            if (utype is UClass uclass)
            {
                Console.WriteLine($"[DEBUG] Проверяем UClass в {path}, ClassName: {GetClassName(uclass)}, Members: {uclass.Members?.Count ?? 0}");

                // Проверяем, является ли этот UClass элементом с _command
                if (HasCommand16(uclass))
                {
                    Console.WriteLine($"[НАЙДЕН] _command = 16 в {path}");

                    // Ищем _args внутри этого же UClass
                    var dialogueStrings = ExtractArgsFromCommand16(uclass);
                    if (dialogueStrings.Count == 2)
                    {
                        string tag = NormalizeString(dialogueStrings[0]);
                        string text = NormalizeString(dialogueStrings[1]);

                        Console.WriteLine($"[ДИАЛОГ] Tag: '{tag}' -> Text: '{text}'");
                        result.Add(new DialogueLine(tag, text));
                    }
                    else if (dialogueStrings.Count > 0)
                    {
                        Console.WriteLine($"[ПРЕДУПРЕЖДЕНИЕ] Найдено {dialogueStrings.Count} строк вместо 2: {string.Join(", ", dialogueStrings.Select(s => $"'{s}'"))}");
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
                Console.WriteLine($"[DEBUG] Проверяем Uarray в {path}, Length: {uarray.Value?.Length ?? 0}");

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
                Console.WriteLine($"[DEBUG] Пропускаем {utype?.GetType().Name} в {path}");
            }
        }

        /// <summary>
        /// Проверяет, содержит ли UClass поле _command со значением 16
        /// </summary>
        private bool HasCommand16(UClass uclass)
        {
            if (uclass?.Members == null || uclass.Members.Count < 5) return false;

            // Из вашего лога видно, что структура Param всегда:
            // [0] _hash (Uint32), [1] _version (Uint32), [2] _multi (Uint8), [3] _command (Uint32), [4] _args (UClass)
            // Проверим, что это именно такая структура
            if (GetClassName(uclass) == "Param" && uclass.Members.Count == 5)
            {
                var commandMember = uclass.Members[3]; // _command всегда на позиции 3
                Console.WriteLine($"[DEBUG] Проверяем member[3] как _command, тип: {commandMember?.GetType().Name}");

                var commandValue = GetRawValue(commandMember);
                Console.WriteLine($"[DEBUG] Значение member[3]: {commandValue} (тип: {commandValue?.GetType().Name})");

                if (commandValue is int intVal)
                {
                    Console.WriteLine($"[DEBUG] _command = {intVal}, ищем 16");
                    if (intVal == 16)
                    {
                        Console.WriteLine($"[DEBUG] ✓ НАЙДЕН _command = 16!");
                        return true;
                    }
                }
            }

            // Оставляем старый способ как резервный
            for (int i = 0; i < uclass.Members.Count; i++)
            {
                var member = uclass.Members[i];
                string memberName = TryGetMemberName(uclass, i);
                Console.WriteLine($"[DEBUG] Проверяем член {i}: name='{memberName}', type={member?.GetType().Name}");

                if (memberName == "_command")
                {
                    var commandValue = GetRawValue(member);
                    Console.WriteLine($"[DEBUG] _command найден через имя! Значение: {commandValue}");

                    if (commandValue is int intVal && intVal == 16)
                    {
                        Console.WriteLine($"[DEBUG] ✓ НАЙДЕН _command = 16 через имя!");
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
                Console.WriteLine($"[DEBUG] _args member[4] тип: {argsMember?.GetType().Name}");

                if (argsMember is UClass argsClass)
                {
                    string className = GetClassName(argsClass);
                    Console.WriteLine($"[DEBUG] _args ClassName: {className}");

                    if (argsClass.Members != null && argsClass.Members.Count > 0)
                    {
                        // Ищем массив внутри vector (обычно в member[0])
                        var vectorMember = argsClass.Members[0];
                        if (vectorMember is Uarray argsArray)
                        {
                            Console.WriteLine($"[DEBUG] Найден массив длины: {argsArray.Value?.Length ?? 0}");

                            // Извлекаем строки из массива
                            if (argsArray.Value != null)
                            {
                                foreach (var element in argsArray.Value)
                                {
                                    string extractedString = ExtractStringFromElement(element);
                                    if (!string.IsNullOrEmpty(extractedString))
                                    {
                                        result.Add(extractedString);
                                        Console.WriteLine($"[DEBUG] Извлечена строка: '{extractedString}'");
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
                    Console.WriteLine($"[DEBUG] Найден _args через имя на позиции {i}");

                    if (argsMember is UClass argsClass)
                    {
                        string className = GetClassName(argsClass);
                        Console.WriteLine($"[DEBUG] _args ClassName: {className}");

                        if (argsClass.Members != null)
                        {
                            for (int j = 0; j < argsClass.Members.Count; j++)
                            {
                                var vectorMember = argsClass.Members[j];
                                if (vectorMember is Uarray argsArray)
                                {
                                    Console.WriteLine($"[DEBUG] Найден массив длины: {argsArray.Value?.Length ?? 0}");

                                    if (argsArray.Value != null)
                                    {
                                        foreach (var element in argsArray.Value)
                                        {
                                            string extractedString = ExtractStringFromElement(element);
                                            if (!string.IsNullOrEmpty(extractedString))
                                            {
                                                result.Add(extractedString);
                                                Console.WriteLine($"[DEBUG] Извлечена строка: '{extractedString}'");
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
                    // Ищем массив байтов внутри string класса
                    foreach (var member in stringClass.Members)
                    {
                        if (member is Uarray byteArray && IsByteArray(byteArray))
                        {
                            return TryDecodeByteArray(byteArray);
                        }
                    }
                }
            }

            // Если это сразу массив байтов
            if (element is Uarray directArray && IsByteArray(directArray))
            {
                return TryDecodeByteArray(directArray);
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
        private void DebugLog(string message)
        {
            if (Program.IsDebugMode)
            {
                Console.WriteLine(message);
            }
        }
    }
}