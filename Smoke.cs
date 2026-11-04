using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Xml.Serialization;
using PhoFit;
class Smoke {
    static void Check(bool b, string what) { if(!b) throw new Exception(what); }
    static void Main() {
        var x=Enumerable.Range(0,201).Select(i=>(double)i).ToArray();
        var y=x.Select(v=>8+200*Math.Exp(-Math.Pow((v-95)/5,2)/2)+120*Math.Exp(-Math.Pow((v-104)/4,2)/2)).ToArray();
        Func<double,string> n=v=>v.ToString("G17",CultureInfo.InvariantCulture);
        File.WriteAllLines("outputs/demo-counts.txt",y.Select(n));
        File.WriteAllLines("outputs/demo-energy.csv",new[]{"energy,counts"}.Concat(x.Select((v,i)=>n(v/2+20)+","+n(y[i]))));
        var s=Spectrum.Read("outputs/demo-counts.txt");
        var energy=Spectrum.Read("outputs/demo-energy.csv");
        Check(s.Y.SequenceEqual(energy.Y) && energy.X[0]==20 && s.X[200]==200,"read formats");
        foreach(int method in new[]{0,1,2}) {
            var r=new Region { Left=60,Right=145,Method=method };
            r.Components.Add(Component.Create("POLY",90,10,1,60,145));
            r.Components.Add(Component.Create("G",94,180,1,60,145));
            r.Components.Add(Component.Create("G",106,105,1,60,145));
            var f=new Fitter(s,r).Run(300);
            Console.WriteLine("Method {0}: converged={1}, objective={2:G5}, centers={3:G9} {4:G9}",method,f.Converged,f.Cost,r.Components[1].P[1].Value,r.Components[2].P[1].Value);
            Check(f.Converged && f.Covariance && f.Cost<1e-7 && Math.Abs(r.Components[1].P[1].Value-95)<0.001 &&
                Math.Abs(r.Components[2].P[1].Value-104)<0.001 && f.Areas[1]>2500 && f.Areas[1]<2510,"fit");
            var p=new Project { Data=s };p.Regions.Add(r);
            var ser=new XmlSerializer(typeof(Project));
            using(var stream=new MemoryStream()) {
                ser.Serialize(stream,p);stream.Position=0;
                var read=(Project)ser.Deserialize(stream);
                Check(read.Data.Y.SequenceEqual(y)&&read.Regions[0].Components.Count==3,"project");
            }
        }
        foreach(string k in Component.Kinds) {
            var c=Component.Create(k,100,100,1,0,200);
            Check(x.All(v=>{var a=c.Eval(v,0,200);return !double.IsNaN(a)&&!double.IsInfinity(a);}),k);
        }
        Console.WriteLine("OK: input, all three fits, project round trip, all 28 model shapes.");
    }
}
