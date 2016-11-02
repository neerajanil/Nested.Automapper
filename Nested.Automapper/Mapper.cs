using Sigil;
using SigilNG = Sigil.NonGeneric;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nested
{

    public static class FunctionGenerator {
        
        private static Dictionary<string, Emit<Func<IDictionary<string, object>, string, object>>> _keyGenerators = new Dictionary<string, Emit<Func<IDictionary<string, object>, string, object>>>();
        private static Dictionary<string, Emit<Func<IDictionary<string, object>, IDictionary<object, object>, string, object>>> _rowMappers = new Dictionary<string, Emit<Func<IDictionary<string, object>, IDictionary<object, object>, string, object>>>();
        private static Dictionary<string, Func<IDictionary<string, object>, IDictionary<object, object>, string, object>> _rowMapperDelegates = new Dictionary<string, Func<IDictionary<string, object>, IDictionary<object, object>, string, object>>();
        private static Dictionary<string, object> _mappers = new Dictionary<string, object>();
        private static Dictionary<string, object> _mapperDelegates = new Dictionary<string, object>();
        

        static MethodInfo StringConcat2 = typeof(string).GetMethod("Concat", new Type[2] { typeof(string), typeof(string) });
        static MethodInfo ChangeType = typeof(System.Convert).GetMethod("ChangeType", new Type[] { typeof(object), typeof(Type) });
        static MethodInfo GetTypeFromhandle = typeof(Type).GetMethod("GetTypeFromHandle", new Type[] { typeof(RuntimeTypeHandle) });
        static MethodInfo DictionaryGet_String_Object = typeof(IDictionary<string, object>).GetMethod("get_Item");
        static MethodInfo DictionaryContains_String_Object = typeof(IDictionary<string, object>).GetMethod("ContainsKey");
        static MethodInfo DictionaryAdd_String_Object = typeof(IDictionary<string, object>).GetMethod("Add");


        static MethodInfo DictionaryGet_Object_Object = typeof(IDictionary<object, object>).GetMethod("get_Item");
        static MethodInfo DictionaryContains_Object_Object = typeof(IDictionary<object, object>).GetMethod("ContainsKey");
        static MethodInfo DictionaryAdd_Object_Object = typeof(IDictionary<object, object>).GetMethod("Add");
        static ConstructorInfo DictionaryConstructor_Object_Object = typeof(Dictionary<object, object>).GetConstructor(new Type[] { });

        

        static MethodInfo IEnumerable_Object_GetEnumerator = typeof(IEnumerable<object>).GetMethod("GetEnumerator", new Type[] { });
        static MethodInfo IEnumerator_Object_GetCurrent = typeof(IEnumerator<object>).GetMethod("get_Current", new Type[] { });

        static MethodInfo IEnumerator_MoveNext = typeof(System.Collections.IEnumerator).GetMethod("MoveNext", new Type[] { });
        static MethodInfo IEnumerator_Dispose = typeof(IDisposable).GetMethod("Dispose", new Type[] { });



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
                var keyProperties = type.MappedProperties().Where(t => t.GetCustomAttributes(typeof(KeyAttribute), false).Count() > 0);
                var keyFields = type.MappedFields().Where(t => t.GetCustomAttributes(typeof(KeyAttribute), false).Count() > 0);
                var keys = keyProperties.Cast<MemberInfo>().Concat(keyFields.Cast<MemberInfo>());
                
                //verify primitivity
                if (keys.Count() == 0)
                {
                    //if atleast one primitive property is not null then return a unique Tuple key
                    var notNullLabel = emiter.DefineLabel("NotNull");
                    var allPrimitiveProperties = type.MappedProperties().Where(t => t.PropertyType.IsPrimitive || t.PropertyType == typeof(string) || t.PropertyType == typeof(Guid) || (t.PropertyType.IsGenericType == true && t.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>)));
                    var allPrimitiveFields = type.MappedFields().Where(t => t.FieldType.IsPrimitive || t.FieldType == typeof(string) || t.FieldType == typeof(Guid) || (t.FieldType.IsGenericType == true && t.FieldType.GetGenericTypeDefinition() == typeof(Nullable<>)));
                    
                    foreach (var property in allPrimitiveProperties.Cast<MemberInfo>().Concat(allPrimitiveFields.Cast<MemberInfo>()))
                    {
                        var tryCatchDone = emiter.DefineLabel("tryCatchDone" + property.Name);
                        var exceptionBlock = emiter.BeginExceptionBlock();// try {
                        emiter.LoadArgument(0); //data
                        emiter.LoadArgument(1);// depthString
                        emiter.LoadConstant(property.Name); //propertyName
                        emiter.Call(StringConcat2); //dataKey = depthString + propertyName
                        emiter.CallVirtual(DictionaryGet_String_Object); // data[dataKey]
                        emiter.LoadNull();
                        emiter.CompareEqual();
                        emiter.BranchIfTrue(tryCatchDone); //if(data[dataKey] != null) goto notNullLabel

                        emiter.Leave(notNullLabel);
                        emiter.MarkLabel(tryCatchDone); // } --end try
                        var catchBlock = emiter.BeginCatchBlock<KeyNotFoundException>(exceptionBlock); // catch(KeynNotFoundException) { -- the column was not found in datarow , continue as if column value was null
                        emiter.Pop(); // remove exception forom stack
                        emiter.EndCatchBlock(catchBlock);
                        emiter.EndExceptionBlock(exceptionBlock); // } --end catch
                    }

                    emiter.LoadNull(); // --code reaches this point if all the primitive properties where null , so return null
                    emiter.Return(); // return null;

                    emiter.MarkLabel(notNullLabel); //notNullLabel : 
                    var constructor = typeof(Tuple<string, object>).GetConstructor(new Type[2] { typeof(string), typeof(object) }); // create a key comprising of the depthstring and a guid , if an object has no key then all rows are considered unique
                    emiter.LoadArgument(1); 
                    var newGuidMethod = typeof(Guid).GetMethod("NewGuid");
                    emiter.Call(newGuidMethod);
                    emiter.Box(typeof(Guid)); // var guid = (object)Guid.NewGuid();
                    emiter.NewObject(constructor); // return new Tuple<string,object>(depthstring,guid);
                    emiter.Return();
                    emiter.CreateDelegate();
                    _keyGenerators.Add(type.FullName, emiter);
                    emit = emiter;
                }
                else
                {
                    //if keys are present create a Tuple made of depthstring and the value of each column marked as key
                    var constructor = GetTupleConstructor(keys.Count());
                    
                    var isNotNullLabel = emiter.DefineLabel("isNotNull");
                    var tryCatchDoneLabel = emiter.DefineLabel("tryCatchDone");
                    var localCompositeKey = emiter.DeclareLocal<object>("compositeKey");
                    var isNullLabels = new List<Sigil.Label>();
                    /* var compositeKey = null;
                     * try{
                     *  var key1 = data[key1Name];
                     *  var key2 = data[key1Name];
                     *  .
                     *  .
                     *  var key6 = data[key1Name];
                     *  compositeKey = new Tuple<string,object,object,...>(depthString, key1, key2...);
                     * }
                     * catch(KeyNotFoundException){
                     *  compositeKey = null;
                     * }
                     * return compositeKey;
                     */
                    var exceptionBlock = emiter.BeginExceptionBlock();
                    emiter.LoadArgument(1);
                    foreach (var property in keys.OrderBy(t => t.Name))
                    {
                        var isNullLabel = emiter.DefineLabel("IsNull" + property.Name);
                        isNullLabels.Add(isNullLabel);
                        emiter.LoadArgument(0); //data
                        emiter.LoadArgument(1);// depthString
                        emiter.LoadConstant(property.Name); //propertyName
                        emiter.Call(StringConcat2); //dataKey = depthString + propertyName
                        emiter.CallVirtual(DictionaryGet_String_Object); // data[dataKey]
                        emiter.Duplicate();
                        emiter.LoadNull(); //null
                        emiter.BranchIfEqual(isNullLabel); // if(localValue == null) goto isNull
                    }

                    emiter.NewObject(constructor);
                    emiter.StoreLocal(localCompositeKey);
                    emiter.Leave(tryCatchDoneLabel);

                    isNullLabels.Reverse();
                    foreach (var label in isNullLabels)
                    {
                        emiter.MarkLabel(label);
                        emiter.Pop();
                    }
                    emiter.Pop();
                    emiter.LoadNull();
                    emiter.StoreLocal(localCompositeKey);

                    var catchBlock = emiter.BeginCatchBlock<KeyNotFoundException>(exceptionBlock);
                    emiter.LoadNull();
                    emiter.StoreLocal(localCompositeKey);
                    emiter.Pop();
                    emiter.EndCatchBlock(catchBlock);
                    emiter.EndExceptionBlock(exceptionBlock);

                    emiter.MarkLabel(tryCatchDoneLabel);
                    emiter.LoadLocal(localCompositeKey);
                    emiter.Return(); // return new Tuple<string,object,object,..>(depthString,data[],data[],..)

                    emiter.CreateDelegate();
                    _keyGenerators.Add(type.FullName, emiter);
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

        public static Emit<Func<IDictionary<string, object>, IDictionary<object, object>, string, object>> GenerateRowMapper(Type type)
        {

            Emit<Func<IDictionary<string, object>, IDictionary<object, object>, string, object>> emit;
            if (_rowMappers.ContainsKey(type.FullName))
            {
                emit = _rowMappers[type.FullName];
            }
            else
            {

                //                     source                      cache                     
                var emiter = Emit<Func<IDictionary<string, object>, IDictionary<object, object>, string, object>>.NewDynamicMethod(type.Name + "RowMapper");
                var keyGenerator = GenerateKeyGenerator(type);
                var isNotNew = emiter.DefineLabel("isNotNew");
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

                emiter.LoadArgument(1);//load cache
                emiter.LoadLocal(keyLocal); //load key
                emiter.Call(DictionaryContains_Object_Object);// cache.Contains(key);

                emiter.BranchIfFalse(isNew);
                emiter.Branch(isNotNew);

                emiter.MarkLabel(isNew);
                emiter.NewObject(type.GetConstructor(new Type[] { }));

                emiter.StoreLocal(newObjectLocal);
                emiter.LoadArgument(1);//load cache
                emiter.LoadLocal(keyLocal);
                emiter.LoadLocal(newObjectLocal);
                emiter.CallVirtual(DictionaryAdd_Object_Object);

                var localString = emiter.DeclareLocal<string>("localString");
                var literalProperties = type.MappedProperties().Where(t => t.PropertyType.IsPrimitive || t.PropertyType == typeof(string) || t.PropertyType == typeof(Guid));
                var literalFields = type.MappedFields().Where(t => t.FieldType.IsPrimitive || t.FieldType == typeof(string) || t.FieldType == typeof(Guid));
                foreach (var property in literalProperties.Cast<MemberInfo>().Concat(literalFields.Cast<MemberInfo>()))
                {
                    var tryCatchDone = emiter.DefineLabel("tryCatchDone" + property.Name);
                    var localData = emiter.DeclareLocal(property.Type(), "localData" + property.Name);

                    var exceptionBlock = emiter.BeginExceptionBlock();
                    emiter.LoadArgument(0);
                    emiter.LoadArgument(2);
                    emiter.LoadConstant(property.Name);
                    emiter.Call(StringConcat2);
                    emiter.CallVirtual(DictionaryGet_String_Object);
                    emiter.LoadConstant(property.Type());
                    emiter.Call(GetTypeFromhandle);
                    emiter.Call(ChangeType);

                    if (property.Type().IsValueType)
                        emiter.UnboxAny(property.Type());
                    else
                        emiter.CastClass(property.Type());

                    emiter.StoreLocal(localData);
                    emiter.Leave(tryCatchDone);
                    var catchBlock = emiter.BeginCatchBlock<KeyNotFoundException>(exceptionBlock);
                    emiter.LoadLocalAddress(localData);
                    emiter.InitializeObject(property.Type());
                    emiter.Leave(tryCatchDone);
                    emiter.EndCatchBlock(catchBlock);
                    emiter.EndExceptionBlock(exceptionBlock);

                    emiter.MarkLabel(tryCatchDone);
                    emiter.LoadLocal(newObjectLocal);
                    emiter.LoadLocal(localData);



                    PropertyInfo pi = property as PropertyInfo;
                    if (pi != null)
                        emiter.Call(pi.SetMethod);
                    else
                    {
                        FieldInfo fi = property as FieldInfo;
                        emiter.StoreField(fi);
                    }

                }

                var nullableProperties = type.MappedProperties().Where(t => t.PropertyType.IsGenericType == true && t.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>));
                var nullableFields = type.MappedFields().Where(t => t.FieldType.IsGenericType == true && t.FieldType.GetGenericTypeDefinition() == typeof(Nullable<>));

                foreach (var property in nullableProperties.Cast<MemberInfo>().Concat(nullableFields.Cast<MemberInfo>()))
                {
                    Type innerType = property.Type().GenericTypeArguments[0];
                    var nullableConstructor = property.Type().GetConstructor(new Type[] { innerType });
                    var nullableNullConstructor = property.Type().GetConstructor(new Type[] { });
                    var isNull = emiter.DefineLabel("IsNull" + property.Name);
                    var isNotNull = emiter.DefineLabel("IsNotNull" + property.Name);
                    var tryCatchDone = emiter.DefineLabel("tryCatchDone" + property.Name);

                    var nullableLocal = emiter.DeclareLocal(property.Type(), "NullableLocal" + property.Name);
                    emiter.LoadLocalAddress(nullableLocal);
                    emiter.InitializeObject(property.Type());

                    var exceptionBlock = emiter.BeginExceptionBlock();
                    emiter.LoadArgument(0);
                    emiter.LoadArgument(2);
                    emiter.LoadConstant(property.Name);
                    emiter.Call(StringConcat2);
                    emiter.StoreLocal(localString);
                    emiter.LoadLocal(localString);
                    emiter.Call(DictionaryContains_String_Object);
                    emiter.BranchIfFalse(isNotNull);



                    emiter.LoadArgument(0);
                    emiter.LoadLocal(localString);
                    emiter.CallVirtual(DictionaryGet_String_Object);
                    emiter.Duplicate();
                    emiter.LoadNull();
                    emiter.BranchIfEqual(isNull);

                    emiter.LoadConstant(innerType);
                    emiter.Call(GetTypeFromhandle);
                    emiter.Call(ChangeType);
                    if (property.Type().IsValueType)
                        emiter.UnboxAny(innerType);
                    else
                        emiter.CastClass(innerType);

                    emiter.NewObject(nullableConstructor);
                    emiter.StoreLocal(nullableLocal);
                    emiter.Branch(isNotNull);

                    emiter.MarkLabel(isNull);
                    emiter.Pop();

                    emiter.MarkLabel(isNotNull);
                    emiter.Leave(tryCatchDone);
                    var catchBlock = emiter.BeginCatchBlock<KeyNotFoundException>(exceptionBlock);
                    emiter.Leave(tryCatchDone);
                    emiter.EndCatchBlock(catchBlock);
                    emiter.EndExceptionBlock(exceptionBlock);

                    emiter.MarkLabel(tryCatchDone);
                    emiter.LoadLocal(newObjectLocal);
                    emiter.LoadLocal(nullableLocal);

                    PropertyInfo pi = property as PropertyInfo;
                    if (pi != null)
                        emiter.Call(pi.SetMethod);
                    else
                    {
                        FieldInfo fi = property as FieldInfo;
                        emiter.StoreField(fi);
                    }

                }

                var nonliteralProperties = type.MappedProperties().Where(t =>
                    !(
                        t.PropertyType.IsPrimitive
                        || t.PropertyType == typeof(string)
                        || t.PropertyType == typeof(Guid)
                        || (t.PropertyType.IsGenericType == true && t.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    ));
                var nonliteralFields = type.MappedFields().Where(t =>
                    !(
                        t.FieldType.IsPrimitive
                        || t.FieldType == typeof(string)
                        || t.FieldType == typeof(Guid)
                        || (t.FieldType.IsGenericType == true && t.FieldType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    ));


                var nonListProperties = nonliteralProperties.Where(t => !typeof(System.Collections.IEnumerable).IsAssignableFrom(t.PropertyType));
                var nonListFields = nonliteralFields.Where(t => !typeof(System.Collections.IEnumerable).IsAssignableFrom(t.FieldType));

                foreach (var property in nonListProperties.Cast<MemberInfo>().Concat(nonListFields.Cast<MemberInfo>()))
                {
                    emiter.LoadLocal(newObjectLocal);
                    emiter.LoadArgument(0);
                    emiter.LoadArgument(1);
                    emiter.LoadArgument(2);
                    emiter.LoadConstant(property.Name + ".");
                    emiter.Call(StringConcat2);
                    emiter.Call(GenerateRowMapper(property.Type()));
                    emiter.CastClass(property.Type());

                    PropertyInfo pi = property as PropertyInfo;
                    if (pi != null)
                        emiter.Call(pi.SetMethod);
                    else
                    {
                        FieldInfo fi = property as FieldInfo;
                        emiter.StoreField(fi);
                    }
                }

                var listProperties = nonliteralProperties.Where(t => typeof(System.Collections.IEnumerable).IsAssignableFrom(t.PropertyType));
                var listFields = nonliteralFields.Where(t => typeof(System.Collections.IEnumerable).IsAssignableFrom(t.FieldType));
                foreach (var property in listProperties.Cast<MemberInfo>().Concat(listFields.Cast<MemberInfo>()))
                {
                    Type innerType = property.Type().GenericTypeArguments[0];
                    Type listType = typeof(List<>).MakeGenericType(innerType);
                    MethodInfo listTypeAdd = listType.GetMethod("Add");
                    ConstructorInfo listTypeConstructor = listType.GetConstructor(new Type[] { });
                    var alreadyCreated = emiter.DefineLabel("AlreadyCreated" + property.Name);
                    var newlyCreated = emiter.DefineLabel("NewlyCreated" + property.Name);

                    var listTypeLocal = emiter.DeclareLocal(listType);
                    emiter.NewObject(listTypeConstructor);
                    emiter.StoreLocal(listTypeLocal);


                    emiter.LoadLocal(listTypeLocal); //List<T> listTypeLocal = new List<T>();
                    emiter.LoadArgument(0); //data
                    emiter.LoadArgument(1); //cache
                    emiter.LoadArgument(2); //depthstring
                    emiter.LoadConstant(property.Name + ".");
                    emiter.Call(StringConcat2);  //fullkey = depthstring + property.Name
                    emiter.Call(GenerateRowMapper(innerType)); //var localObject = mapper(data,cache,fullkey)
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

                    PropertyInfo pi = property as PropertyInfo;
                    if (pi != null)
                        emiter.Call(pi.SetMethod);
                    else
                    {
                        FieldInfo fi = property as FieldInfo;
                        emiter.StoreField(fi);
                    }

                }


                emiter.LoadLocal(newObjectLocal);
                emiter.Return();

                emiter.MarkLabel(isNotNew);
                emiter.LoadArgument(1);
                emiter.LoadLocal(keyLocal);
                emiter.Call(DictionaryGet_Object_Object);
                emiter.CastClass(type);
                emiter.StoreLocal(newObjectLocal);

                foreach (var property in listProperties.Cast<MemberInfo>().Concat(listFields.Cast<MemberInfo>()))
                {
                    Type innerType = property.Type().GenericTypeArguments[0];
                    Type listType = typeof(List<>).MakeGenericType(innerType);
                    MethodInfo listTypeAdd = listType.GetMethod("Add");
                    var alreadyCreated = emiter.DefineLabel("AlreadyCreated1" + property.Name);
                    var newlyCreated = emiter.DefineLabel("NewlyCreated1" + property.Name);

                    emiter.LoadLocal(newObjectLocal);

                    PropertyInfo pi = property as PropertyInfo;
                    if (pi != null)
                        emiter.Call(pi.GetMethod);
                    else
                    {
                        FieldInfo fi = property as FieldInfo;
                        emiter.LoadField(fi);
                    }


                    emiter.LoadArgument(0);
                    emiter.LoadArgument(1);
                    emiter.LoadArgument(2);
                    emiter.LoadConstant(property.Name + ".");
                    emiter.Call(StringConcat2);
                    emiter.Call(GenerateRowMapper(innerType));
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

                _rowMappers.Add(type.FullName, emiter);
                //emiter.CreateDelegate();
                _rowMapperDelegates.Add(type.FullName, emiter.CreateDelegate());
                emit = emiter;
            }
            return emit;
        }

        public static Emit<Func<IEnumerable<object>, IEnumerable<T>>> GenerateMapper<T>()
        {
            Type type = typeof(T);
            Emit<Func<IEnumerable<object>, IEnumerable<T>>> emit = null;
            if (_mappers.ContainsKey(type.FullName))
            {
                emit = (Emit<Func<IEnumerable<object>, IEnumerable<T>>>)_mappers[type.FullName];
            }
            else
            {

                ConstructorInfo listConstructor = typeof(List<T>).GetConstructor(new Type[] { });
                MethodInfo listAdd = typeof(List<T>).GetMethod("Add", new Type[] { typeof(T) });

                Emit<Func<IEnumerable<object>, IEnumerable<T>>> emiter = Emit<Func<IEnumerable<object>, IEnumerable<T>>>.NewDynamicMethod(type.Name + "Mapper");


                var enumeratorLocal = emiter.DeclareLocal<IEnumerator<object>>("enumerator");
                var listLocal = emiter.DeclareLocal<List<T>>("list");
                var cacheLocal = emiter.DeclareLocal<IDictionary<object, object>>("cache");


                var loopFinishedLabel = emiter.DefineLabel("loopFinished");
                var loopCheckLabel = emiter.DefineLabel("loopCheck");
                var loopBeginLabel = emiter.DefineLabel("loopBegin");
                var finallyFinishedLabel = emiter.DefineLabel("finallyFinished");
                var isNullLabel = emiter.DefineLabel("isNull");


                emiter.NewObject(listConstructor);
                emiter.StoreLocal(listLocal);
                emiter.NewObject(DictionaryConstructor_Object_Object);
                emiter.StoreLocal(cacheLocal);

                emiter.LoadArgument(0);
                emiter.CallVirtual(IEnumerable_Object_GetEnumerator);
                emiter.StoreLocal(enumeratorLocal);

                //try {
                var exceptionBlock = emiter.BeginExceptionBlock();
                emiter.Branch(loopCheckLabel);

                emiter.MarkLabel(loopBeginLabel);

                emiter.LoadLocal(listLocal);
                emiter.LoadLocal(enumeratorLocal);
                emiter.CallVirtual(IEnumerator_Object_GetCurrent);
                emiter.CastClass<IDictionary<string, object>>();
                emiter.LoadLocal(cacheLocal);
                emiter.LoadConstant("");
                emiter.Call(GenerateRowMapper(type)); // var rowResult = rowMapper ( row, cache, depthString = "" );
                emiter.Duplicate();
                emiter.LoadNull();
                emiter.BranchIfEqual(isNullLabel);


                emiter.CastClass(typeof(T));
                emiter.Call(listAdd); // listLocal.Add((T)rowResult);
                emiter.Branch(loopCheckLabel);

                emiter.MarkLabel(isNullLabel);
                emiter.Pop();
                emiter.Pop();


                emiter.MarkLabel(loopCheckLabel);
                emiter.LoadLocal(enumeratorLocal);
                emiter.CallVirtual(IEnumerator_MoveNext);
                emiter.BranchIfTrue(loopBeginLabel);
                emiter.Leave(loopFinishedLabel);
                //}
                //finallY {
                var finallyBlock = emiter.BeginFinallyBlock(exceptionBlock);
                emiter.LoadNull();
                emiter.LoadLocal(enumeratorLocal);
                emiter.CompareEqual();
                emiter.BranchIfTrue(finallyFinishedLabel);
                emiter.LoadLocal(enumeratorLocal);
                emiter.CallVirtual(IEnumerator_Dispose);
                emiter.MarkLabel(finallyFinishedLabel);
                emiter.EndFinallyBlock(finallyBlock);
                emiter.EndExceptionBlock(exceptionBlock);
                //}

                emiter.MarkLabel(loopFinishedLabel);
                emiter.LoadLocal(listLocal);
                emiter.Return(); // return listLocal;
                _mapperDelegates.Add(type.FullName, emiter.CreateDelegate());
                _mappers.Add(type.FullName, emiter);
                emit = emiter;
            }
            return emit;
        }

        public static Func<IEnumerable<object>, IEnumerable<T>> GetMapperDelegate<T>()
        {
            Type t = typeof(T);
            if (!_mapperDelegates.ContainsKey(t.FullName))
            {
                GenerateMapper<T>();
            }
            return (Func<IEnumerable<object>, IEnumerable<T>>)_mapperDelegates[t.FullName];
        }

    }

    public static class Automapper
    {
        


        public static IEnumerable<T> Map<T>(IEnumerable<IDictionary<string, object>> source)
        {
            return FunctionGenerator.GetMapperDelegate<T>()(source);
        }


        public static IEnumerable<T> Map<T>(IEnumerable<object> source)
        {
            return FunctionGenerator.GetMapperDelegate<T>()(source);
        }

    }

    internal static class MappingHelper
    {
        internal static Type Type(this MemberInfo mi)
        {
            PropertyInfo pi = mi as PropertyInfo;
            if (pi != null)
            {
                return pi.PropertyType;
            }
            else
            {
                FieldInfo fi = mi as FieldInfo;
                return fi.FieldType;
            }
        }

        internal static IEnumerable<PropertyInfo> MappedProperties(this Type type)
        {
            return type.GetProperties().Where(t => !t.GetCustomAttributes(typeof(NotMappedAttribute), false).Any());
        }

        internal static IEnumerable<FieldInfo> MappedFields(this Type type)
        {
            return type.GetFields().Where(t => !t.GetCustomAttributes(typeof(NotMappedAttribute), false).Any());
        }
        
    }
}
