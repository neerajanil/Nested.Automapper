using Sigil;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Nested.Automapper
{
    public static class Mapper
    {
        static MethodInfo StringConcat2 = typeof(string).GetMethod("Concat", new Type[2] { typeof(string), typeof(string) });
        
        static MethodInfo DictionaryGet_String_Object = typeof(IDictionary<string,object>).GetMethod("get_Item");
        static MethodInfo DictionaryContains_String_Object = typeof(IDictionary<string, object>).GetMethod("ContainsKey");
        static MethodInfo DictionaryAdd_String_Object = typeof(IDictionary<string, object>).GetMethod("Add");


        static MethodInfo DictionaryGet_Object_Object = typeof(IDictionary<object,object>).GetMethod("get_Item");
        static MethodInfo DictionaryContains_Object_Object = typeof(IDictionary<object, object>).GetMethod("ContainsKey");
        static MethodInfo DictionaryAdd_Object_Object = typeof(IDictionary<object, object>).GetMethod("Add");

        private static Dictionary<string, Emit<Func<IDictionary<string, object>, string, object>>> _keyGenerators = new Dictionary<string, Emit<Func<IDictionary<string, object>, string, object>>>();

        private static Dictionary<string, Emit<Func<IDictionary<string, object>, IDictionary<object, object>, string, object>>> _mappers = new Dictionary<string, Emit<Func<IDictionary<string, object>, IDictionary<object, object>, string, object>>>();
        



        public static Emit<Func<IDictionary<string, object>, string, object>> GenerateKeyGenerator(Type type)
        {
            Emit<Func<IDictionary<string, object>, string, object>> emit;
            if (_keyGenerators.ContainsKey(type.FullName.ToString()))
            {
                emit = _keyGenerators[type.FullName];
            }
            else
            {
                var emiter = Emit<Func<IDictionary<string, object>, string, object>>.NewDynamicMethod(type.Name + "KeyGenerator");
                var keyProperties = type.GetProperties().Where(t => t.GetCustomAttributes(typeof(KeyAttribute), false).Count() > 0);
                if (keyProperties.Count() == 0)
                {
                    var constructor = typeof(Tuple<string, object>).GetConstructor(new Type[2] { typeof(string), typeof(object) });
                    emiter.LoadArgument(1);
                    var newGuidMethod = typeof(Guid).GetMethod("NewGuid");
                    emiter.Call(newGuidMethod);
                    emiter.Box(typeof(Guid));
                    emiter.NewObject(constructor);
                    emiter.Return();
                    emiter.CreateDelegate();
                    emit = emiter;
                }
                else
                {
                    var constructor = GetTupleConstructor(keyProperties.Count());
                    emiter.LoadArgument(1);
                    foreach (var property in keyProperties)
                    {
                        emiter.LoadArgument(0);
                        emiter.LoadArgument(1);
                        emiter.LoadConstant(property.Name);
                        emiter.Call(StringConcat2);
                        emiter.CallVirtual(DictionaryGet_String_Object);
                    }
                    emiter.NewObject(constructor);
                    emiter.Return();
                    emiter.CreateDelegate();
                    emit = emiter;
                    
                }
            }
            
            return emit;
        }

        private static ConstructorInfo GetTupleConstructor(int i)
        {
            switch (i)
            {
                case 1: return typeof(Tuple<string, object>).GetConstructors()[0];
                case 2: return typeof(Tuple<string, object, object>).GetConstructors()[0];
                case 3: return typeof(Tuple<string, object, object, object>).GetConstructors()[0];
                case 4: return typeof(Tuple<string, object, object, object, object>).GetConstructors()[0];
                case 5: return typeof(Tuple<string, object, object, object, object, object>).GetConstructors()[0];
                case 6: return typeof(Tuple<string, object, object, object, object, object, object>).GetConstructors()[0];
                default: throw new ArgumentOutOfRangeException();
            }
        }

        public static Emit<Func<IDictionary<string, object>, IDictionary<object, object>, string, object>> GenerateMapper(Type type)
        {

            Emit<Func<IDictionary<string, object>, IDictionary<object, object>, string, object>> emit;
            if (_mappers.ContainsKey(type.FullName))
            {
                emit = _mappers[type.FullName];
            }
            else
            {

                //                     source                      dump                     
                var emiter = Emit<Func<IDictionary<string, object>, IDictionary<object, object>, string, object>>.NewDynamicMethod(type.Name + "Mapper");
                var keyGenerator = GenerateKeyGenerator(type);
                var isInDictionary = emiter.DefineLabel("ifInDictionart");  // names are purely for ease of debugging, and are optional
                var isNew = emiter.DefineLabel("IfNew");
                var newObjectLocal = emiter.DeclareLocal(type, "NewObject");
                var keyLocal = emiter.DeclareLocal(typeof(object), "keyLocal");

                emiter.LoadArgument(1);//load dump
                emiter.LoadArgument(0);//load rowdata
                emiter.LoadArgument(2);//load depthString
                emiter.Call(keyGenerator);// keygenerator(rowdata,depthstring)
                emiter.StoreLocal(keyLocal);
                emiter.LoadLocal(keyLocal);
                emiter.Call(DictionaryContains_Object_Object);// dump.Contains(key);

                emiter.BranchIfFalse(isNew);
                emiter.Branch(isInDictionary);

                emiter.MarkLabel(isNew);
                emiter.NewObject(type.GetConstructor(new Type[] { }));

                emiter.StoreLocal(newObjectLocal);
                emiter.LoadArgument(1);//load dump
                emiter.LoadLocal(keyLocal);
                emiter.LoadLocal(newObjectLocal);
                emiter.CallVirtual(DictionaryAdd_Object_Object);

                var literalProperties = type.GetProperties().Where(t => t.PropertyType.IsPrimitive || t.PropertyType.Name == "String" || t.PropertyType.Name == "Guid");
                foreach (var property in literalProperties)
                {
                    emiter.LoadLocal(newObjectLocal);
                    emiter.LoadArgument(0);
                    emiter.LoadArgument(2);
                    emiter.LoadConstant(property.Name);
                    emiter.Call(StringConcat2);
                    emiter.CallVirtual(DictionaryGet_String_Object);
                    if (property.PropertyType.IsValueType)
                        emiter.UnboxAny(property.PropertyType);
                    else
                        emiter.CastClass(property.PropertyType);
                    emiter.Call(property.SetMethod);
                }

                var nonliteralProperties = type.GetProperties().Where(t => !(t.PropertyType.IsPrimitive || t.PropertyType.Name == "String" || t.PropertyType.Name == "Guid"));

                var nonListProperties = nonliteralProperties.Where(t => !typeof(System.Collections.IEnumerable).IsAssignableFrom(t.PropertyType));
                foreach (var property in nonListProperties) {
                    emiter.LoadLocal(newObjectLocal);
                    emiter.LoadArgument(0);
                    emiter.LoadArgument(1);
                    emiter.LoadArgument(2);
                    emiter.LoadConstant(property.Name + ".");
                    emiter.Call(StringConcat2);
                    emiter.Call(GenerateMapper(property.PropertyType));
                    emiter.CastClass(property.PropertyType);
                    emiter.Call(property.SetMethod);
                }

                var listProperties = nonliteralProperties.Where(t => typeof(System.Collections.IEnumerable).IsAssignableFrom(t.PropertyType));
                foreach (var property in listProperties)
                {
                    Type innerType = property.PropertyType.GenericTypeArguments[0];
                    Type listType = typeof(List<>).MakeGenericType(innerType);
                    MethodInfo listTypeAdd = listType.GetMethod("Add");
                    ConstructorInfo listTypeConstructor = listType.GetConstructor(new Type[]{});

                    var listTypeLocal = emiter.DeclareLocal(listType);
                    emiter.NewObject(listTypeConstructor);
                    emiter.StoreLocal(listTypeLocal);

                    
                    emiter.LoadLocal(listTypeLocal);
                    emiter.LoadArgument(0);
                    emiter.LoadArgument(1);
                    emiter.LoadArgument(2);
                    emiter.LoadConstant(property.Name + ".");
                    emiter.Call(StringConcat2);
                    emiter.Call(GenerateMapper(innerType));
                    emiter.CastClass(innerType);
                    emiter.Call(listTypeAdd);

                    emiter.LoadLocal(newObjectLocal);
                    emiter.LoadLocal(listTypeLocal);
                    emiter.Call(property.SetMethod);

                }


                emiter.LoadLocal(newObjectLocal);
                emiter.Return();
                emiter.MarkLabel(isInDictionary);
                emiter.LoadNull();
                emiter.Return();
                emiter.CreateDelegate();
                emit = emiter;
            }
            return emit;
        }


        public static IEnumerable<T> Map<T>(IEnumerable<IDictionary<string, object>> source)
        {
            var t = typeof(T);

            foreach (var row in source)
            {

            }

            return new List<T>();
        }
    }
}
