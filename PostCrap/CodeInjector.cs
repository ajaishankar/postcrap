using System;
using System.Linq;
using System.IO;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using PostCrap.Nihl;
using PostCrap.Nihl.Internal;
using FieldAttributes=Mono.Cecil.FieldAttributes;
using MethodAttributes=Mono.Cecil.MethodAttributes;
using TypeAttributes=Mono.Cecil.TypeAttributes;

namespace PostCrap
{
	public class CodeInjector
	{
		private readonly AssemblyDefinition _assembly;

		private readonly TypeReference _objType;
		private readonly TypeReference _objArrayType;
		private readonly TypeReference _voidType;
		private readonly TypeReference _methodInfoType;
		private readonly TypeReference _interceptorArrayType;
		private readonly TypeReference _methodInvokerType;
		private readonly TypeReference _invocationType;
		
		public CodeInjector(AssemblyDefinition assembly)
		{
			_assembly = assembly;
			
			_objType = ImportType<object>();
			_objArrayType = ImportType<object[]>();
			_voidType = ImportType(typeof(void));

			_methodInfoType = ImportType<MethodInfo>();
			_interceptorArrayType = ImportType<IInterceptor[]>();
			_methodInvokerType = ImportType<MethodInvoker>();
			_invocationType = ImportType<Invocation>();			
		}

		private TypeReference ImportType<T>()
		{
			return _assembly.MainModule.Import(typeof(T));
		}

		private TypeReference ImportType(Type type)
		{
			return _assembly.MainModule.Import(type);
		}

		private MethodReference ImportConstructor<T>(params Type[] types)
		{
			return _assembly.MainModule.Import(typeof (T).GetConstructor(types));
		}

		private MethodReference ImportConstructor<T>()
		{
			return _assembly.MainModule.Import(typeof (T).GetConstructors().First(c => !c.IsStatic));
		}

		private MethodReference ImportMethod<T>(string methodName, BindingFlags bindingFlags)
		{
			return _assembly.MainModule.Import(typeof(T).GetMethod(methodName, bindingFlags));
		}

		private MethodReference ImportMethod<T>(string methodName, Type[] types)
		{
			return _assembly.MainModule.Import(typeof(T).GetMethod(methodName, types));
		}

		private MethodReference ImportMethod<T>(string methodName)
		{
			return _assembly.MainModule.Import(typeof(T).GetMethod(methodName));
		}

		private MethodReference ImportPropertyGetter<T>(string propertyName)
		{
			return _assembly.MainModule.Import(typeof (T).GetProperty(propertyName).GetGetMethod());
		}

		private MethodReference ImportMethod(Type type, string methodName, BindingFlags bindingFlags)
		{
			return _assembly.MainModule.Import(type.GetMethod(methodName, bindingFlags));
		}

		public static void ProcessAssembly(string sourcePath, string destinationPath)
		{
			AssemblyDefinition assembly = AssemblyFactory.GetAssembly(sourcePath);

			((BaseAssemblyResolver)assembly.Resolver).AddSearchDirectory(Path.GetDirectoryName(sourcePath));
			
			new CodeInjector(assembly).InjectStubs();

			AssemblyFactory.SaveAssembly(assembly, destinationPath);
		}

		private void InjectStubs()
		{
			TypeReference interceptorAttribute = ImportType<InterceptorAttribute>();

			foreach (TypeDefinition type in _assembly.MainModule.Types)
			{
				var groups = (from method in type.Methods.OfType<MethodDefinition>()
				              where ShouldIntercept(method, interceptorAttribute)
				              group method by method.Name
				              into g
				              	select new {Name = g.Key, Methods = g.ToArray()}).ToArray();

				foreach (var g in groups)
				{
					for(int i = 0; i < g.Methods.Length; ++i)
					{
						Stub(g.Methods[i], i);
					}
				}
			}
		}

		private static bool ShouldIntercept(MethodDefinition method, TypeReference interceptorAttribute)
		{
			foreach(CustomAttribute attribute in method.CustomAttributes)
			{
				TypeDefinition type = attribute.Constructor.DeclaringType.Resolve();

				while(true)
				{
					if (type.FullName == interceptorAttribute.FullName)
						return true;

					if(type.BaseType == null)
						break;

					type = type.BaseType.Resolve();
				}
			}

			return false;
		}

		private static FieldReference GetFieldReference(FieldReference field)
		{
			if (!field.DeclaringType.HasGenericParameters)
				return field;

			var gen = new GenericInstanceType(field.DeclaringType);

			foreach (GenericParameter t in field.DeclaringType.GenericParameters)
			{
				gen.GenericArguments.Add(t);
			}

			return new FieldReference(field.Name, gen, field.FieldType);
		}

		private static MethodReference GetMethodReference(MethodReference method)
		{
			if (!method.DeclaringType.HasGenericParameters)
				return method;

			var gen = new GenericInstanceType(method.DeclaringType);

			foreach (GenericParameter t in method.DeclaringType.GenericParameters)
			{
				gen.GenericArguments.Add(t);
			}

			var m = new MethodReference(method.Name, gen, method.ReturnType.ReturnType, method.HasThis, method.ExplicitThis,
									method.CallingConvention);

			foreach (ParameterDefinition p in method.Parameters)
			{
				m.Parameters.Add(p.Clone());
			}

			return m;
		}

		private static GenericInstanceType GetGenericInstanceType(TypeReference genericType)
		{
			var type = new GenericInstanceType(genericType);
			foreach (GenericParameter t in genericType.GenericParameters)
			{
				type.GenericArguments.Add(t);
			}
			return type;
		}

		private TypeDefinition StubType(string name, MethodDefinition method, MethodDefinition callOriginal)
		{
			var stubType = new TypeDefinition(
				name, "",
				TypeAttributes.NestedPrivate,
				_objType);

			_assembly.MainModule.Types.Add(stubType);
			method.DeclaringType.NestedTypes.Add(stubType);

			foreach (GenericParameter type in method.DeclaringType.GenericParameters)
			{
				stubType.GenericParameters.Add(new GenericParameter(type.Name, stubType)
				                               	{
													Position = type.Position,
													Attributes = type.Attributes
				                               	});
			}

			stubType.Methods.Add(callOriginal);
			
			var ctor = new MethodDefinition(".ctor",
											 MethodAttributes.Private | MethodAttributes.HideBySig |
											 MethodAttributes.SpecialName
											 | MethodAttributes.RTSpecialName, _voidType);

			var cil = ctor.Body.CilWorker;

			cil.Emit(OpCodes.Ldarg_0);
			cil.Emit(OpCodes.Call, ImportConstructor<object>());
			cil.Emit(OpCodes.Ret);

			stubType.Constructors.Add(ctor);

			
			var cctor = new MethodDefinition(".cctor",
			                                 MethodAttributes.Private | MethodAttributes.HideBySig |
			                                 MethodAttributes.SpecialName
			                                 | MethodAttributes.RTSpecialName | MethodAttributes.Static, _voidType);

			stubType.Constructors.Add(cctor);

			const FieldAttributes attributes = FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.InitOnly;

			var methodField = new FieldDefinition("Method", _methodInfoType, attributes);
			var interceptorsField = new FieldDefinition("Interceptors", _interceptorArrayType, attributes);
			var invokerField = new FieldDefinition("Invoker", _methodInvokerType, attributes);

			stubType.Fields.Add(methodField);
			stubType.Fields.Add(interceptorsField);
			stubType.Fields.Add(invokerField);

			MethodReference getMethodFromHandle = ImportMethod<MethodBase>("GetMethodFromHandle",
															   new Type[] { typeof(RuntimeMethodHandle) });

			MethodReference getMethodFromHandle2 = ImportMethod<MethodBase>("GetMethodFromHandle",
												   new Type[] { typeof(RuntimeMethodHandle), typeof(RuntimeTypeHandle) });

			MethodReference getInterceptorsFor = ImportMethod<InterceptorAttribute>("GetInterceptorsFor",
			                                                                        BindingFlags.Static | BindingFlags.Public);

			cil = cctor.Body.CilWorker;

			// initialize static fields
			cil.Emit(OpCodes.Ldtoken, method);
			if (method.DeclaringType.HasGenericParameters)
			{
				var type = GetGenericInstanceType(method.DeclaringType);
				cil.Emit(OpCodes.Ldtoken, type);
				cil.Emit(OpCodes.Call, getMethodFromHandle2);
			}
			else
			{
				cil.Emit(OpCodes.Call, getMethodFromHandle);
			}


			cil.Emit(OpCodes.Castclass, _methodInfoType);
			cil.Emit(OpCodes.Stsfld, GetFieldReference(methodField));

			cil.Emit(OpCodes.Ldsfld, GetFieldReference(methodField));
			cil.Emit(OpCodes.Call, getInterceptorsFor);
			cil.Emit(OpCodes.Stsfld, GetFieldReference(interceptorsField));

			cil.Emit(OpCodes.Ldnull);
			cil.Emit(OpCodes.Ldftn, GetMethodReference(callOriginal));
			cil.Emit(OpCodes.Newobj, ImportConstructor<MethodInvoker>(typeof(object), typeof(IntPtr)));
			cil.Emit(OpCodes.Stsfld, GetFieldReference(invokerField));

			cil.Emit(OpCodes.Ret);

			return stubType;
		}

		private MethodDefinition CallOriginal(MethodDefinition targetMethod)
		{
			var openUnbox = ImportMethod<StubHelper>("Unbox");

			var callOriginal = new MethodDefinition("CallOriginal",
			                                      MethodAttributes.Private | MethodAttributes.HideBySig |
			                                      MethodAttributes.Static, _objType);

			var targetParam = new ParameterDefinition(_objType);
			var argsParam = new ParameterDefinition(_objArrayType);

			callOriginal.Parameters.Add(targetParam);
			callOriginal.Parameters.Add(argsParam);

			var cil = callOriginal.Body.CilWorker;

			if (!targetMethod.IsStatic)
			{
				cil.Emit(OpCodes.Ldarg, targetParam);

				if (targetMethod.DeclaringType.HasGenericParameters)
				{
					var type = GetGenericInstanceType(targetMethod.DeclaringType);
					cil.Emit(OpCodes.Castclass, type);
				}
				else
				{
					cil.Emit(OpCodes.Castclass, targetMethod.DeclaringType);
				}
			}
			
			// unbox arguments
			for (int i = 0; i < targetMethod.Parameters.Count; ++i)
			{				
				var paramType = targetMethod.Parameters[i].ParameterType;

				cil.Emit(OpCodes.Ldarg, argsParam);
				cil.Emit(OpCodes.Ldc_I4, i);
				cil.Emit(OpCodes.Ldelem_Ref);

				var unbox = new GenericInstanceMethod(openUnbox);
				unbox.GenericArguments.Add(paramType);

				cil.Emit(OpCodes.Call, unbox);
			}

			// call
			cil.Emit(OpCodes.Call, GetMethodReference(targetMethod));

			var returnType = targetMethod.ReturnType.ReturnType;

			if (returnType.Equals(_voidType))
			{
				cil.Emit(OpCodes.Ldnull);
			}
			else if (returnType.IsValueType || returnType is GenericParameter)
			{
				cil.Emit(OpCodes.Box, returnType);
			}

			cil.Emit(OpCodes.Ret);

			return callOriginal;
		}

		private static FieldDefinition Field(TypeDefinition type, string fieldName)
		{
			return type.Fields.OfType<FieldDefinition>().FirstOrDefault(f => f.Name == fieldName);
		}

		private void Stub(MethodDefinition method, int index)
		{
			string name = method.Name + (index == 0 ? "" : index.ToString());

			MethodDefinition target = method.Clone();
			target.Overrides.Clear(); // clear explicit interface implementations
			target.Name = name + "_original";

			method.DeclaringType.Methods.Add(target);

			MethodDefinition callOriginal = CallOriginal(target);

			TypeDefinition stubType = StubType(name + "_stub", method, callOriginal);

			CilWorker cil = method.Body.CilWorker;

			method.Body.Variables.Clear();
			method.Body.Instructions.Clear();
			method.Body.InitLocals = true;

			var result = new VariableDefinition(_objType);
			method.Body.Variables.Add(result);

			var invocation = new VariableDefinition(_invocationType);
			method.Body.Variables.Add(invocation);

			var args = new VariableDefinition(_objArrayType);
			method.Body.Variables.Add(args);

			// init args[]
			cil.Emit(OpCodes.Ldc_I4, method.Parameters.Count);
			cil.Emit(OpCodes.Newarr, _objType);
			cil.Emit(OpCodes.Stloc, args);

			for(int i = 0; i < method.Parameters.Count; ++i)
			{
				cil.Emit(OpCodes.Ldloc, args);
				cil.Emit(OpCodes.Ldc_I4, i);
				cil.Emit(OpCodes.Ldarg, i + (method.IsStatic ? 0 : 1));

				var paramType = method.Parameters[i].ParameterType;

				if(paramType.IsValueType || paramType is GenericParameter)
				{
					cil.Emit(OpCodes.Box, paramType);
				}

				cil.Emit(OpCodes.Stelem_Ref);
			}

			var methodField = GetFieldReference(Field(stubType, "Method"));
			var interceptorsField = GetFieldReference(Field(stubType, "Interceptors"));
			var invokerField = GetFieldReference(Field(stubType, "Invoker"));

			cil.Emit(OpCodes.Ldsfld, interceptorsField);
			cil.Emit(OpCodes.Ldsfld, methodField);
			cil.Emit(OpCodes.Ldsfld, invokerField);

			if (method.IsStatic)
				cil.Emit(OpCodes.Ldnull);
			else
				cil.Emit(OpCodes.Ldarg_0);

			cil.Emit(OpCodes.Ldloc, args);

			// new Invocation(...).Proceed
			cil.Emit(OpCodes.Newobj, ImportConstructor<Invocation>());
			cil.Emit(OpCodes.Stloc, invocation);			
			cil.Emit(OpCodes.Ldloc, invocation);
			cil.Emit(OpCodes.Callvirt, ImportMethod<Invocation>("Proceed"));

			var getResult = ImportPropertyGetter<Invocation>("Result");

			cil.Emit(OpCodes.Ldloc, invocation);
			cil.Emit(OpCodes.Callvirt, getResult);
			cil.Emit(OpCodes.Stloc, result);

			TypeReference returnType = method.ReturnType.ReturnType;

			if (!returnType.Equals(_voidType))
			{
				var openUnbox = ImportMethod<StubHelper>("Unbox");
				var unbox = new GenericInstanceMethod(openUnbox);
				unbox.GenericArguments.Add(returnType);

				cil.Emit(OpCodes.Ldloc, result);
				cil.Emit(OpCodes.Call, unbox);
			}

			cil.Emit(OpCodes.Ret);			
		}
	}
}
