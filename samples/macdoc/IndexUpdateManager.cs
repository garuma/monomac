using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;

using Monodoc;

namespace macdoc
{
	public class IndexUpdateManager
	{
		readonly string baseUserDir; // This is e.g. .config/MonoDoc, a user-specific place where we can write stuff
		readonly IEnumerable<string> sourceFiles; // this is $prefix/monodoc/sources folder
		
		public event EventHandler UpdaterChange;
		
		public IndexUpdateManager (IEnumerable<string> sourceFiles, string baseUserDir)
		{
			Console.WriteLine ("Going to verify [{0}]", sourceFiles.Aggregate ((e1, e2) => e1 + ", " + e2));
			this.baseUserDir = baseUserDir;
			this.sourceFiles = sourceFiles;
		}
		
		public Task<bool> CheckIndexIsFresh ()
		{
			return Task.Factory.StartNew (() => {
				Dictionary<string, string> md5sums = null;
				var path = Path.Combine (baseUserDir, "index_freshness");
				
				if (File.Exists (path)) {
					try {
						md5sums = DeserializeDictionary (path);
					} catch {}
				}
				if (md5sums == null)
					md5sums = new Dictionary<string, string> ();
				
				bool isFresh = true;
				HashAlgorithm hasher = MD5.Create ();
				
				foreach (var source in sourceFiles) {
					var hash = StringHash (hasher, File.OpenRead (source));
					string originalHash;
					if (md5sums.TryGetValue (source, out originalHash))
						isFresh = originalHash.Equals (hash, StringComparison.OrdinalIgnoreCase);
					else
						isFresh = false;
					md5sums[source] = hash;
				}
				
				SerializeDictionary (path, md5sums);
				Console.WriteLine ("We have a {0} fresh index", isFresh);
				
				return isFresh;
			});
		}
		
		string StringHash (HashAlgorithm hasher, Stream stream)
		{
			return hasher.ComputeHash (stream).Select (b => String.Format("{0:X2}", b)).Aggregate (string.Concat);
		}
		
		Dictionary<string, string> DeserializeDictionary (string path)
		{
			if (!File.Exists (path))
				return new Dictionary<string, string> ();
			return File.ReadAllLines (path)
				.Where (l => !string.IsNullOrEmpty (l) && l[0] != '#') // Take non-empty, non-comment lines
				.Select (l => l.Split ('='))
				.Where (a => a != null && a.Length == 2)
				.ToDictionary (t => t[0].Trim (), t => t[1].Trim ());
		}
		
		void SerializeDictionary (string path, Dictionary<string, string> dict)
		{
			File.WriteAllLines (path, dict.Select (kvp => string.Format ("{0} = {1}", kvp.Key, kvp.Value)));
		}
		
		public void PerformSearchIndexCreation ()
		{
			FireSearchIndexCreationEvent (true);
			RootTree.MakeSearchIndex ();
			FireSearchIndexCreationEvent (false);
		}
		
		void FireSearchIndexCreationEvent (bool status)
		{
			IsCreatingSearchIndex = status;
			Thread.MemoryBarrier ();
			var evt = UpdaterChange;
			if (evt != null)
				evt (this, EventArgs.Empty);
		}
		
		public bool IsCreatingSearchIndex {
			get;
			set;
		}
	}
}

