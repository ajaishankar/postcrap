using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;

namespace PostCrap.Tests
{
	[TestFixture]
	public class InjectorTests
	{
		private AppDomain _appDomain;
		private string _assemblyPath;
		private InterceptMe _intercepted;

		[TestFixtureSetUp]
		public void Setup()
		{
			string sourcePath = new Uri(typeof (InjectorTests).Assembly.CodeBase).LocalPath;
			string directory = Path.GetDirectoryName(sourcePath);
			_assemblyPath = Path.Combine(directory, "PostCrap.Tests.crap.dll");

			if(File.Exists(_assemblyPath))
				File.Delete(_assemblyPath);

			string exe = Path.Combine(
				directory.Replace("PostCrap.Tests", "PostCrap"), "postcrap.exe");

			var p = Process.Start(
				Quote(exe), Quote(sourcePath) + " " + Quote(_assemblyPath));

			if(p != null)
				p.WaitForExit();

			if (!File.Exists(_assemblyPath))
				throw new Exception("PostCrap crapped out");

			_appDomain = AppDomain.CreateDomain("test domain");
			_intercepted = CreateInstanceAndUnwrap<InterceptMe>();
		}

		public T CreateInstanceAndUnwrap<T>()
		{
			return (T)_appDomain.CreateInstanceFromAndUnwrap(_assemblyPath, typeof(T).FullName);
		}

		[TestFixtureTearDown]
		public void Teardown()
		{
			AppDomain.Unload(_appDomain);
		}

		[Test]
		[ExpectedException(typeof(NotImplementedException))]
		public void Foo_Will_Throw_Exception_If_Not_Intercepted()
		{
			new InterceptMe().WithEatExceptionAndLogNotImplemented(1, 2);
		}

		[Test]
		public void Will_Inject_EatException_And_Log_Interceptors()
		{
			int result = _intercepted.WithEatExceptionAndLogNotImplemented(1, 2);
			Assert.AreEqual(42, result);
		}

		[Test]
		public void Will_Inject_Two_IncrementArg_Interceptors()
		{
			int result = _intercepted.WithTwoIncrementArg(0);
			Assert.AreEqual(2, result);
		}

		[Test]
		public void Will_Inject_Abort_Interceptor()
		{
			int result = _intercepted.WithAbort();
			Assert.AreEqual(911, result);
		}

		[Test]
		public void Injection_Can_Handle_Static_Methods()
		{
			int result = _intercepted.Invoke_WithIncrementArgStatic(1);
			Assert.AreEqual(2, result);
		}

		[Test]
		public void Injection_Can_Handle_Nulls()
		{
			string result = _intercepted.WithIncrementArgTestNull(0, null);
			Assert.AreEqual("1", result);
		}

		[Test]
		public void Can_Intercept_Method_In_GenericType_Of_Int_String_WithIncrementArg()
		{
			var helper = CreateInstanceAndUnwrap<GenericHelper>();
			int result = helper.Invoke_WithIncrementArg_On_Int_String(1, "it works!");
			Assert.AreEqual(2, result);
		}

		[Test]
		public void Can_Intercept_GenericType_Of_Float_String_WithIncrementArg()
		{
			var helper = CreateInstanceAndUnwrap<GenericHelper>();
			float result = helper.Invoke_WithIncrementArg_On_Float_String(1, "it works!");
			Assert.AreEqual(2, result);
		}

		[Test]
		public void Can_Intercept_Method_In_GenericType_Of_Int_String_WithIncrementArg_In_Nested_Type()
		{
			var helper = CreateInstanceAndUnwrap<GenericHelper>();
			int result = helper.Invoke_WithIncrementArg_On_Int_String_Nested(1, "it works!");
			Assert.AreEqual(2, result);
		}

		private static string Quote(string value)
		{
			return '"' + value + '"';
		}
	}
}
