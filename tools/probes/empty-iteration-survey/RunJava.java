// java.util.regex battery runner; launch with: java RunJava.java <dir>
import java.nio.file.*; import java.util.regex.*;
public class RunJava {
  public static void main(String[] a) throws Exception {
    for (String line : Files.readAllLines(Path.of(a[0], "battery.tsv"))) {
      if (line.isBlank() || line.startsWith("#")) continue;
      String[] f = line.split("\t", -1);
      String r;
      Matcher m = Pattern.compile(f[1]).matcher(f[2]);
      if (m.find()) r = "span=" + m.start() + "," + m.end() + " g1=" + (m.group(1) == null ? "unset" : "'" + m.group(1) + "'");
      else r = "nomatch";
      System.out.println("java " + Runtime.version() + "\t" + f[0] + "\t" + r);
    }
  }
}
