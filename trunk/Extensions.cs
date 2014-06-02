using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Styx.Common;

namespace HighVoltz.AutoAngler
{
	static class Extensions
	{
		public static CircularQueue<T> ToCircularQueue<T>(this IEnumerable<T> source)
		{
			var result = new CircularQueue<T>();
			source.ForEach(result.Add);
			return result;
		}
	}
}
