using System;
using System.Reflection;

namespace PostCrap.Nihl
{
	public interface IInterceptor
	{
		void Intercept(Invocation invocation);
	}

	[Serializable]
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
	public class InterceptorAttribute : Attribute, IInterceptor
	{
		public int Order { get; set; }

		void IInterceptor.Intercept(Invocation invocation)
		{
			OnInvocation(invocation);
		}

		public virtual void OnInvocation(Invocation invocation) {}

		public static IInterceptor[] GetInterceptorsFor(MethodInfo method)
		{
			var attribs = GetCustomAttributes(method, typeof (InterceptorAttribute), true);
			var interceptors = new IInterceptor[attribs.Length];

			for (var i = 0; i < attribs.Length; ++i)
				interceptors[i] = (IInterceptor) attribs[i];

			Array.Sort(interceptors, (x, y) =>
			                         ((InterceptorAttribute) x).Order.CompareTo(((InterceptorAttribute) y).Order));

			return interceptors;
		}
	}
}