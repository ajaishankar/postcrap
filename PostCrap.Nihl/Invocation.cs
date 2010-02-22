using System.Reflection;

namespace PostCrap.Nihl
{
	public delegate object MethodInvoker(object target, object[] args);

	public sealed class Invocation
	{
		private readonly IInterceptor[] _interceptors;
		private readonly MethodInvoker _methodInvoker;
		private int _index = -1;
		private bool _cancelled;

		public MethodInfo Method { get; private set; }
		public object Target { get; private set; }
		public object[] Arguments { get; private set; }
		public object Result { get; set; }

		public Invocation(IInterceptor[] interceptors, MethodInfo method,
		                  MethodInvoker invoker, object target, object[] args)
		{
			_interceptors = interceptors;
			_methodInvoker = invoker;
			Method = method;
			Target = target;
			Arguments = args;
		}

		public void Proceed()
		{
			start:
			if (_cancelled || _index > _interceptors.Length)
				return;

			_index += 1;

			if (_index == _interceptors.Length)
			{
				Result = _methodInvoker(Target, Arguments);
				return;
			}

			int current = _index;
			_interceptors[current].Intercept(this);
			if (current == _index) // did not proceed inside interceptor
			{
				goto start;
			}
		}

		public void Cancel()
		{
			_cancelled = true;
		}
	}
}