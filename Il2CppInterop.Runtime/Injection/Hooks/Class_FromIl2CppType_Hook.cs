using System;
using System.Linq;
using System.Runtime.InteropServices;
using Il2CppInterop.Common;
using Il2CppInterop.Common.XrefScans;
using Il2CppInterop.Runtime.Runtime;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Runtime.Injection.Hooks
{
    internal unsafe class Class_FromIl2CppType_Hook : Hook<Class_FromIl2CppType_Hook.MethodDelegate>
    {
        public override string TargetMethodName => "Class::FromIl2CppType";
        public override MethodDelegate GetDetour() => Hook;

        /// Common version of the Il2CppType, the only thing that changed between unity version are the bitfields values that we don't use
        internal readonly struct Il2CppType
        {
            public readonly void* data;
            public readonly ushort attrs;
            public readonly Il2CppTypeEnum type;
            private readonly byte _bitfield;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate Il2CppClass* MethodDelegate(Il2CppType* type, bool throwOnError);

        private Il2CppClass* Hook(Il2CppType* type, bool throwOnError)
        {
            // Handle injected non-generic classes
            if ((nint)type->data < 0 && (type->type == Il2CppTypeEnum.IL2CPP_TYPE_CLASS || type->type == Il2CppTypeEnum.IL2CPP_TYPE_VALUETYPE))
            {
                InjectorHelpers.s_InjectedClasses.TryGetValue((nint)type->data, out var classPointer);
                return (Il2CppClass*)classPointer;
            }

            // Handle injected generic instantiations
            if (type->type == Il2CppTypeEnum.IL2CPP_TYPE_GENERICINST)
            {
                var genericClass = (Il2CppGenericClass*)type->data;

                // Check if this references an injected generic definition (negative index)
                if (genericClass->typeDefinitionIndex < 0 &&
                    ClassInjector._injectedGenericIndexToType.TryGetValue(genericClass->typeDefinitionIndex, out var genericDefType))
                {
                    // Check if we have a cached class
                    if (genericClass->cached_class != null)
                        return genericClass->cached_class;

                    // Try to build the closed type from the type arguments
                    var typeArgs = new Type[genericClass->context.class_inst->type_argc];
                    for (int i = 0; i < typeArgs.Length; i++)
                    {
                        var argType = genericClass->context.class_inst->type_argv[i];
                        typeArgs[i] = ClassInjector.SystemTypeFromIl2CppType(argType);
                    }

                    var closedType = genericDefType.MakeGenericType(typeArgs);
                    var result = ClassInjector.InstantiateInjectedGeneric(closedType);
                    if (result != IntPtr.Zero)
                    {
                        genericClass->cached_class = (Il2CppClass*)result;
                        return (Il2CppClass*)result;
                    }
                }
            }

            return Original(type, throwOnError);
        }

        public override IntPtr FindTargetMethod()
        {
            var classFromTypeAPI = InjectorHelpers.GetIl2CppExport(nameof(IL2CPP.il2cpp_class_from_il2cpp_type));
            Logger.Instance.LogTrace("il2cpp_class_from_il2cpp_type: 0x{ClassFromTypeApiAddress}", classFromTypeAPI.ToInt64().ToString("X2"));

            return XrefScannerLowLevel.JumpTargets(classFromTypeAPI).Single();
        }
    }
}
