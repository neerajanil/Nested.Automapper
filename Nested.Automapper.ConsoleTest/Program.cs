using Sigil;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nested.Automapper.ConsoleTest
{
    class Program
    {
        static void Main(string[] args)
        {
            List<Dictionary<string, object>> data = new List<Dictionary<string, object>>()
            {
                
                new Dictionary<string, object>()
                {
                    {"C", "data"},
                    {"A.Data","data1"},
                    {"A.Data1",1},
                    {"B.Data","data3"},
                    {"B.Data1",1},
                    {"D.Data","data5"},
                    {"D.Data1",1}
                },
                new Dictionary<string, object>()
                {
                    {"C", "data"},
                    {"A.Data","data1"},
                    {"A.Data1","data2"},
                    {"B.Data","data3"},
                    {"B.Data1","data4"},
                    {"D.Data","data7"},
                    {"D.Data1","data8"}
                },
                new Dictionary<string, object>()
                {
                    {"C", "data"},
                    {"A.Data","data1"},
                    {"A.Data1","data2"},
                    {"B.Data","data9"},
                    {"B.Data1","data10"},
                    {"D.Data","data5"},
                    {"D.Data1","data6"}
                },
                new Dictionary<string, object>()
                {
                    {"C", "data"},
                    {"A.Data","data1"},
                    {"A.Data1","data2"},
                    {"B.Data","data9"},
                    {"B.Data1","data10"},
                    {"D.Data","data7"},
                    {"D.Data1","data8"}
                }
            };

            var s = typeof(string).Name;
            s = typeof(Guid).Name;
            //var z = typeof(Tuple<string, object>).GetConstructor(new Type[2]{ typeof(string),typeof(object) });
            //var z1 = typeof(Dictionary<string, object>).GetMethod("get_Item");
            //var z2 = typeof(string).GetMethod("Concat", new Type[2] { typeof(string), typeof(string) });
            //var delegater = Mapper.GenerateKeyGenerator(typeof(TestType1));
            //object key = delegater.CreateDelegate()(data[0], "");


            var t = typeof(TestType1).GetProperty("B");

            var mapper = Mapper.GenerateMapper(typeof(TestType1));
            var dump = new Dictionary<object, object>();
            var result = mapper.CreateDelegate()(data[0], dump, "");


            //var mm = data[0]["ah"];

            //// Create a delegate that sums two integers
            //var emiter = Emit<Func<int, int, int>>.NewDynamicMethod("MyMethod");
            //emiter.LoadArgument(0);
            //emiter.LoadArgument(1);
            //emiter.Add();
            //emiter.Return();
            //var del = emiter.CreateDelegate();

            //// prints "473"
            //Console.WriteLine(del(314, 159));


            var flag = typeof(System.Collections.IEnumerable).IsAssignableFrom(t.PropertyType);
            flag = t.PropertyType.IsAssignableFrom(typeof(IEnumerable<>));
            
        }
    }

    public class TestType1
    {
        [Key]
        public string C { get; set; }

        public TestType5 A { get; set; }

        public List<TestType5> B { get; set; }
        public List<TestType6> D { get; set; }

    }

    //public class TestType2
    //{
    //    public TestType3 D { get; set; }

    //    public List<TestType4> C { get; set; }

    //}
    //public class TestType3
    //{
    //    public List<TestType2> B { get; set; }

    //}

    public class TestType4
    {
        [Key]
        public string Data { get; set; }

        public int Data1 { get; set; }
    }

    public class TestType5
    {
        [Key]
        public string Data { get; set; }

        public int Data1 { get; set; }
    }

    public class TestType6
    {
        [Key]
        public string Data { get; set; }

        public int Data1 { get; set; }
    }
}
