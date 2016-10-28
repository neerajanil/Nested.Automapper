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
                //verify primitivity
                if (keyProperties.Count() == 0)
                {
                    var notNullLabel = emiter.DefineLabel("NotNull");
                    var allProperties = type.GetProperties().Where(t => t.PropertyType.IsPrimitive || t.PropertyType.Name == "String" || t.PropertyType.Name == "Guid");
                    foreach (var property in keyProperties)
                    {
                        emiter.LoadArgument(0); //data
                        emiter.LoadArgument(1);// depthString
                        emiter.LoadConstant(property.Name); //propertyName
                        emiter.Call(StringConcat2); //dataKey = depthString + propertyName
                        emiter.CallVirtual(DictionaryGet_String_Object); // data[dataKey]
                        emiter.LoadNull();
                        emiter.CompareEqual();
                        emiter.BranchIfFalse(notNullLabel); //if(data[dataKey] != null) goto notNullLabel
                    }

                    emiter.LoadNull();
                    emiter.Return();

                    emiter.MarkLabel(notNullLabel);
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
                    emiter.LoadArgument(1); //depthString
                    
                    Dictionary<string,Sigil.Label> isNullLabels = new Dictionary<string,Sigil.Label>(); //
                    foreach (var property in keyProperties.OrderBy(t => t.Name))
                    {
                        var label = emiter.DefineLabel("IsNull" + property.Name);
                        isNullLabels.Add(property.Name, label);
                        emiter.LoadArgument(0); //data
                        emiter.LoadArgument(1);// depthString
                        emiter.LoadConstant(property.Name); //propertyName
                        emiter.Call(StringConcat2); //dataKey = depthString + propertyName
                        emiter.CallVirtual(DictionaryGet_String_Object); // data[dataKey]
                        emiter.Duplicate();
                        emiter.LoadNull(); //null
                        emiter.BranchIfEqual(label); // if(localValue == null) goto isNull
                    }
                    emiter.NewObject(constructor);
                    emiter.Return(); // return new Tuple<string,object,object,..>(depthString,data[],data[],..)
                    foreach (var property in keyProperties.OrderByDescending(t => t.Name))
                    {
                        emiter.MarkLabel(isNullLabels[property.Name]);
                        emiter.Pop();
                    }
                    
                    emiter.Pop();
                    emiter.LoadNull();
                    emiter.Return(); // return null
                    
                    
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
                var isInDictionary = emiter.DefineLabel("ifInDictionary");  // names are purely for ease of debugging, and are optional
                var isNew = emiter.DefineLabel("IfNew");
                var keyIsNull = emiter.DefineLabel("KeyIsNull");
                var newObjectLocal = emiter.DeclareLocal(type, "NewObject");
                var keyLocal = emiter.DeclareLocal(typeof(object), "keyLocal");

                
                emiter.LoadArgument(0);//load rowdata
                emiter.LoadArgument(2);//load depthString
                emiter.Call(keyGenerator);// keygenerator(rowdata,depthstring)
                emiter.StoreLocal(keyLocal); //store key

                emiter.LoadLocal(keyLocal); //load key
                emiter.LoadNull(); //load null
                emiter.BranchIfEqual(keyIsNull); //if key == null goto keyIsNull

                emiter.LoadArgument(1);//load dump
                emiter.LoadLocal(keyLocal); //load key
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

                var literalProperties = type.GetProperties().Where(t => t.PropertyType.IsPrimitive || t.PropertyType == typeof(string) || t.PropertyType == typeof(Guid));
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

                var nullableProperties = type.GetProperties().Where(t => t.PropertyType.IsGenericType == true && t.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>));
                foreach (var property in nullableProperties)
                {
                    Type innerType = property.PropertyType.GenericTypeArguments[0];
                    var nullableConstructor = property.PropertyType.GetConstructor(new Type[] { innerType });
                    var nullableNullConstructor = property.PropertyType.GetConstructor(new Type[] { });
                    var isNull = emiter.DefineLabel("IsNull" + property.Name);
                    var isNotNull = emiter.DefineLabel("IsNotNull" + property.Name);

                    var nullableLocal = emiter.DeclareLocal(property.PropertyType, "NullableLocal" + property.Name);
                    emiter.LoadLocalAddress(nullableLocal);
                    emiter.InitializeObject(property.PropertyType);

                    
                    emiter.LoadArgument(0);
                    emiter.LoadArgument(2);
                    emiter.LoadConstant(property.Name);
                    emiter.Call(StringConcat2);
                    emiter.CallVirtual(DictionaryGet_String_Object);
                    emiter.Duplicate();
                    emiter.LoadNull();
                    emiter.BranchIfEqual(isNull);


                    if (property.PropertyType.IsValueType)
                        emiter.UnboxAny(innerType);
                    else
                        emiter.CastClass(innerType);
                    emiter.NewObject(nullableConstructor);
                    emiter.StoreLocal(nullableLocal);
                    emiter.Branch(isNotNull);

                    emiter.MarkLabel(isNull);
                    emiter.Pop();
                    

                    emiter.MarkLabel(isNotNull);
                    emiter.LoadLocal(newObjectLocal);
                    emiter.LoadLocal(nullableLocal);
                    emiter.Call(property.SetMethod);
                }

                var nonliteralProperties = type.GetProperties().Where(t => 
                    !(
                        t.PropertyType.IsPrimitive 
                        || t.PropertyType == typeof(string) 
                        || t.PropertyType == typeof(Guid)
                        || (t.PropertyType.IsGenericType == true && t.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>)) 
                    ));
                

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
                    var alreadyCreated = emiter.DefineLabel("AlreadyCreated" + property.Name);
                    var newlyCreated = emiter.DefineLabel("NewlyCreated" + property.Name);

                    var listTypeLocal = emiter.DeclareLocal(listType);
                    emiter.NewObject(listTypeConstructor);
                    emiter.StoreLocal(listTypeLocal);

                    
                    emiter.LoadLocal(listTypeLocal); //List<T> listTypeLocal = new List<T>();
                    emiter.LoadArgument(0); //data
                    emiter.LoadArgument(1); //dump
                    emiter.LoadArgument(2); //depthstring
                    emiter.LoadConstant(property.Name + ".");
                    emiter.Call(StringConcat2);  //fullkey = depthstring + property.Name
                    emiter.Call(GenerateMapper(innerType)); //var localObject = mapper(data,dump,fullkey)
                    emiter.Duplicate();
                    emiter.LoadNull();
                    emiter.BranchIfEqual(alreadyCreated);

                    emiter.CastClass(innerType);
                    emiter.Call(listTypeAdd); //listtypeLocal.Add((T)localObject);
                    emiter.Branch(newlyCreated);
                    
                    emiter.MarkLabel(alreadyCreated);
                    emiter.Pop();
                    emiter.Pop();

                    emiter.MarkLabel(newlyCreated);
                    emiter.LoadLocal(newObjectLocal);
                    emiter.LoadLocal(listTypeLocal);
                    emiter.Call(property.SetMethod);

                }


                emiter.LoadLocal(newObjectLocal);
                emiter.Return();
                
                emiter.MarkLabel(isInDictionary);
                emiter.LoadArgument(1);
                emiter.LoadLocal(keyLocal);
                emiter.Call(DictionaryGet_Object_Object);
                emiter.CastClass(type);
                emiter.StoreLocal(newObjectLocal);
                
                foreach (var property in listProperties)
                {
                    Type innerType = property.PropertyType.GenericTypeArguments[0];
                    Type listType = typeof(List<>).MakeGenericType(innerType);
                    MethodInfo listTypeAdd = listType.GetMethod("Add");
                    var alreadyCreated = emiter.DefineLabel("AlreadyCreated1" + property.Name);
                    var newlyCreated = emiter.DefineLabel("NewlyCreated1" + property.Name);

                    emiter.LoadLocal(newObjectLocal);
                    emiter.Call(property.GetMethod);
                    emiter.LoadArgument(0);
                    emiter.LoadArgument(1);
                    emiter.LoadArgument(2);
                    emiter.LoadConstant(property.Name + ".");
                    emiter.Call(StringConcat2);
                    emiter.Call(GenerateMapper(innerType));
                    emiter.Duplicate();
                    emiter.LoadNull();
                    emiter.BranchIfEqual(alreadyCreated);
                    emiter.CastClass(innerType);
                    emiter.Call(listTypeAdd);
                    emiter.Branch(newlyCreated);
                    emiter.MarkLabel(alreadyCreated);
                    emiter.Pop();
                    emiter.Pop();
                    emiter.MarkLabel(newlyCreated);
                }
                emiter.LoadNull();
                emiter.Return();

                emiter.MarkLabel(keyIsNull);
                emiter.LoadNull();
                emiter.Return();
                
                emiter.CreateDelegate();
                emit = emiter;
            }
            return emit;
        }


        public static IEnumerable<T> Map<T>(IEnumerable<IDictionary<string, object>> source)
        {
            var mapper = GenerateMapper(typeof(T)).CreateDelegate();
            var dump = new Dictionary<object,object>();
            var result = new List<T>();
            foreach (var row in source)
            {
                var m = mapper(row, dump, "");
                if (m != null)
                    result.Add((T)m);
            }

            return result;
        }
    }
}
