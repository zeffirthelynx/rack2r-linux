using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

class Program {
    static void Main(string[] args) {
        string linuxDllPath = args[0];
        string modDllPath = args[1];
        string outDllPath = args[2];

        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(System.IO.Path.GetDirectoryName(linuxDllPath));
        var readerParams = new ReaderParameters { AssemblyResolver = resolver };

        var linuxAsm = AssemblyDefinition.ReadAssembly(linuxDllPath, readerParams);
        var modAsm = AssemblyDefinition.ReadAssembly(modDllPath, readerParams);

        var linuxBeautify = linuxAsm.MainModule.GetType("BeautifyEffect.Beautify");
        var modBeautify = modAsm.MainModule.GetType("BeautifyEffect.Beautify");

        string[] missingFields = { "_depthOfFieldForegroundBlurHQ", "_antialiasStrength", "_antialiasDepthThreshold" };
        foreach (var fieldName in missingFields) {
            var srcField = linuxBeautify.Fields.FirstOrDefault(f => f.Name == fieldName);
            if (srcField != null && !modBeautify.Fields.Any(f => f.Name == fieldName)) {
                int targetIndex = linuxBeautify.Fields.IndexOf(srcField);
                var clonedField = new FieldDefinition(srcField.Name, srcField.Attributes, modAsm.MainModule.ImportReference(srcField.FieldType));
                foreach (var attr in srcField.CustomAttributes) {
                    var newAttr = new CustomAttribute(modAsm.MainModule.ImportReference(attr.Constructor));
                    foreach (var arg in attr.ConstructorArguments) newAttr.ConstructorArguments.Add(arg);
                    clonedField.CustomAttributes.Add(newAttr);
                }
                modBeautify.Fields.Insert(targetIndex, clonedField);
                Console.WriteLine("[+] added sera: " + fieldName);
            }
        }


        var onEnable = modBeautify.Methods.First(m => m.Name == "OnEnable");
        var il = onEnable.Body.GetILProcessor();
        var editorCall = onEnable.Body.Instructions.FirstOrDefault(i => i.Operand != null && i.Operand.ToString().Contains("EditorPrefs"));
        if (editorCall != null) {

            var updateCall = onEnable.Body.Instructions.First(i => i.Operand != null && i.Operand.ToString().Contains("UpdateMaterialPropertiesNow"));
            while (updateCall.Next != null && updateCall.Next.OpCode != OpCodes.Ret) {
                il.Remove(updateCall.Next);
            }
            Console.WriteLine("[+] removed UnityEditor call Beautify.OnEnable");
        }

        modAsm.Write(outDllPath);
        Console.WriteLine("[OK] patched dll can be found at " + outDllPath);
    }
}
