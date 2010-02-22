using System.Reflection;

namespace PostCrap.Nihl.Internal
{
	public class StubHelper
	{
		public static T Unbox<T>(object value)
		{
			return value == null ? default(T) : (T) value;
		}
	}
}
