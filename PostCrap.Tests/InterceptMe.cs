using System;
using System.Linq;
using System.Reflection;
using PostCrap.Nihl;

namespace PostCrap.Tests
{
	public class InterceptMe : MarshalByRefObject
	{
		[EatException(Order = 2)]
		[Log(Order = 1)]
		public int WithEatExceptionAndLogNotImplemented(int x, float y)
		{
			throw new NotImplementedException();
		}

		[IncrementArg]
		[IncrementArg]
		public int WithTwoIncrementArg(int x)
		{
			return x;
		}

		[Abort]
		public int WithAbort()
		{
			return 10;
		}

		[IncrementArg]
		public static int WithIncrementArgTestStatic(int x)
		{
			return x;
		}

		// to easily call static method from different app domain
		public int Invoke_WithIncrementArgStatic(int x)
		{
			return WithIncrementArgTestStatic(x);
		}

		[IncrementArg]
		public string WithIncrementArgTestNull(int? x, string y)
		{
			return "" + x + y;
		}
	}

	public class InterceptMe<T, U> : MarshalByRefObject
	{
		[IncrementArg]
		public T WithIncrementArg(T a1, U u)
		{
			return a1;
		}

		public class Nested
		{
			[IncrementArg]
			public T WithIncrementArg(T a1, U u)
			{
				return a1;
			}
		}
	}

	public class GenericHelper : MarshalByRefObject
	{
		public int Invoke_WithIncrementArg_On_Int_String(int x, string y)
		{
			return new InterceptMe<int, string>().WithIncrementArg(x, y);
		}

		public int Invoke_WithIncrementArg_On_Int_String_Nested(int x, string y)
		{
			return new InterceptMe<int, string>.Nested().WithIncrementArg(x, y);
		}

		public float Invoke_WithIncrementArg_On_Float_String(float x, string y)
		{
			return new InterceptMe<float, string>().WithIncrementArg(x, y);
		}
	}

	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
	public class EatExceptionAttribute : InterceptorAttribute
	{
		public override void OnInvocation(Invocation invocation)
		{
			try
			{
				invocation.Proceed();
			}
			catch
			{
			}

			invocation.Result = 42;
		}
	}

	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
	public class LogAttribute : InterceptorAttribute
	{
		public override void OnInvocation(Invocation invocation)
		{
			invocation.Proceed();

			string args = string.Join(", ",
									  invocation.Arguments
										.Select(arg => arg == null ? "null" : arg.ToString())
										.ToArray());

			Console.WriteLine("{0}({1}) : {2}",
							  invocation.Method.Name, args, invocation.Result ?? "null");
		}
	}

	[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
	public class IncrementArgAttribute : InterceptorAttribute
	{
		public override void OnInvocation(Invocation invocation)
		{
			Type type = invocation.Arguments[0].GetType();

			int value = Convert.ToInt32(invocation.Arguments[0]);
			value += 1;
			invocation.Arguments[0] = Convert.ChangeType(value, type);
		}
	}

	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
	public class AbortAttribute : InterceptorAttribute
	{
		public override void OnInvocation(Invocation invocation)
		{
			invocation.Result = 911;
			invocation.Cancel();
		}
	}
}
