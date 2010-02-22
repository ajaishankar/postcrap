using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PostCrap.Nihl;

namespace PostCrap.Tests
{
	[TestFixture]
	public class InterceptorTests
	{
		[Test]
		public void Can_Get_Interceptors_In_Correct_Order()
		{
			var method = typeof (InterceptMe).GetMethod("WithEatExceptionAndLogNotImplemented");
			var interceptors = InterceptorAttribute.GetInterceptorsFor(method);
			Assert.AreEqual(2, interceptors.Length);
			Assert.IsInstanceOf(typeof(LogAttribute), interceptors[0]);
			Assert.IsInstanceOf(typeof(EatExceptionAttribute), interceptors[1]);
		}

		[Test]
		public void Can_Intercept_Instance_Method()
		{
			var method = typeof(InterceptMe).GetMethod("WithEatExceptionAndLogNotImplemented");
			var interceptors = InterceptorAttribute.GetInterceptorsFor(method);
			MethodInvoker invoker = (o, a) => ((InterceptMe) o).WithEatExceptionAndLogNotImplemented((int) a[0], (int) a[1]);

			var target = new InterceptMe();
			var args = new object[] {1, 2};
			
			var invocation = new Invocation(interceptors, method, invoker, target, args);

			invocation.Proceed();

			Assert.AreEqual(42, invocation.Result<int>());
		}

		[Test]
		public void Can_Intercept_Static_Method()
		{
			var method = typeof (InterceptMe).GetMethod("WithIncrementArgTestStatic", 
				BindingFlags.Public | BindingFlags.Static);
			var interceptors = InterceptorAttribute.GetInterceptorsFor(method);
			MethodInvoker invoker = (o, a) => InterceptMe.WithIncrementArgTestStatic((int)a[0]);

			const InterceptMe target = null;
			var args = new object[] { 1 };

			var invocation = new Invocation(interceptors, method, invoker, target, args);

			invocation.Proceed();

			Assert.AreEqual(2, invocation.Result<int>());
		}

		[Test]
		public void Will_Invoke_All_Interceptors_Even_When_They_Dont_Not_Call_Proceed()
		{
			var method = typeof (InterceptMe).GetMethod("WithTwoIncrementArg");
			var interceptors = InterceptorAttribute.GetInterceptorsFor(method);
			MethodInvoker invoker = (o, a) => ((InterceptMe)o).WithTwoIncrementArg((int)a[0]);

			var target = new InterceptMe();
			var args = new object[] { 0 };

			var invocation = new Invocation(interceptors, method, invoker, target, args);
			
			invocation.Proceed();

			Assert.AreEqual(2, invocation.Result<int>());
		}

		[Test]
		public void Can_Cancel_Invocation()
		{
			var method = typeof (InterceptMe).GetMethod("WithAbort");
			var interceptors = InterceptorAttribute.GetInterceptorsFor(method);
			const MethodInvoker invoker = null;

			var target = new InterceptMe();
			var args = new object[] {};

			var invocation = new Invocation(interceptors, method, invoker, target, args);

			invocation.Proceed();

			Assert.AreEqual(911, invocation.Result<int>());
		}

		[Test]
		public void Can_Handle_Nulls()
		{
			var method = typeof(InterceptMe).GetMethod("WithIncrementArgTestNull");
			var interceptors = InterceptorAttribute.GetInterceptorsFor(method);
			MethodInvoker invoker = (o, a) => ((InterceptMe) o).WithIncrementArgTestNull((int?) a[0], (string) a[1]);

			var target = new InterceptMe();
			var args = new object[] { 0, null };

			var invocation = new Invocation(interceptors, method, invoker, target, args);

			invocation.Proceed();

			Assert.AreEqual("1", invocation.Result<string>());
		}
	}

	public static class InvocationExtensions
	{
		public static T Result<T>(this Invocation invocation)
		{
			object result = invocation.Result;

			if(result == null)
				return default(T);
			else
				return (T)result;
		}
	}
}
