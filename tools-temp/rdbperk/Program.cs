using System; using System.Linq; using System.Text; using System.IO;
var c = new AODB.RdbController(@"I:\AO\client-ddogs");
var sb = new StringBuilder();
foreach (int id in c.RecordTypeToId[1000036].Keys.OrderBy(k=>k)) {
  byte[] raw = c.GetRaw(1000036,id); if (raw==null || raw.Length<36) continue;
  int[] f = Enumerable.Range(0,9).Select(i=>BitConverter.ToInt32(raw,i*4)).ToArray();
  if ((f[5] & 0xC0)==0) continue;
  sb.AppendLine(string.Join("\t", f) + (raw.Length>36 ? "\tlen"+raw.Length : ""));
}
File.WriteAllText(@"I:\AO\src\tools-temp\rdbperk\research.tsv", sb.ToString());
Console.WriteLine("done");
