using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Configuration;
using System.Xml;
using System.IO;
using System.Diagnostics;

namespace MethodExecutor
{
    /// <summary>
    /// 从assembly的xml结构中读取的xml信息
    /// </summary>
    public class XmlMethodInfo
    {
        public string NamespaceName { get; set; }
        public string ClassName { get; set; }
        public string MethodName { get; set; }

        public string MethodFullName { get; set; }

        public string Summary { get; set; }
        public Dictionary<string, string> TypeparamNameCommentDict = new Dictionary<string, string>();
        public Dictionary<string, string> ParamNameCommentDict = new Dictionary<string, string>();
        public string ReturnComment { get; set; }

        public MethodInfo RelatedMethodInfo { get; set; }

        public string GetRealMethodName()
        {
            if (Regex.IsMatch(MethodName, ".+``[0-9]"))  // generic method match
            {
                return MethodName.Substring(0, MethodName.Count() - 3);
            }
            else
            {
                return MethodName;
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(string.Format("{0}.{1}", ClassName, GetRealMethodName()));
            if (!string.IsNullOrEmpty(Summary))
            {
                sb.AppendLine("Summary:");
                sb.AppendLine(string.Format("\t{0}", Summary.Trim()));
            }
            sb.AppendLine("Param Names:");
            foreach(var item in TypeparamNameCommentDict)
            {
                string tempLine = string.Format("\t{0}\t{1}", item.Key, item.Value);
                sb.AppendLine(tempLine);
            }
            foreach(var item in ParamNameCommentDict)
            {
                string tempLine = string.Format("\t{0}\t{1}", item.Key, item.Value);
                sb.AppendLine(tempLine);
            }
            if (!string.IsNullOrEmpty(ReturnComment))
            {
                sb.AppendLine("Returns:");
                sb.AppendLine(string.Format("\t{0}", ReturnComment));
            }

            string temp = TypeHelper.MethodInfo2String(RelatedMethodInfo);
            sb.Append(temp);
            return sb.ToString();
        }

    }

    /// <summary>
    /// Method Info: 存储method运行时需要的各类信息。
    /// </summary>
    public class MyMethod
    {
        public string AssemblyName { get; set; }
        public string NamespaceName { get; set; }
        public string ClassName {get;set;}
        public string MethodName { get; set; }
        public MethodInfo TheMethodInfo { get; set; }
        public List<Type> GenericMethodTypes { get; set; }
        public List<Tuple<string, Type>> ParamsValueTypeTuple { get; set; }
        public bool HasDefaultValue { get; set; }

        public MyMethod()
        {
            AssemblyName = string.Empty;
            NamespaceName = string.Empty;
            ClassName = string.Empty;
            MethodName = string.Empty;
            GenericMethodTypes = new List<Type>();
            ParamsValueTypeTuple = new List<Tuple<string, Type>>();
        }

        public object Execute()
        {
            ParameterInfo[] paramInfos = TheMethodInfo.GetParameters();
            //if (ParamsValueTypeTuple.Count() != paramInfos.Count())
            //{
            //    LogHelper.PrintError("Input params count is not consistent!");
            //    return null;
            //}

            List<object> realParams = new List<object>();

            for (int i = 0; i < ParamsValueTypeTuple.Count(); i++)
            {
                Type pInfoType = paramInfos[i].ParameterType;
                Type pInputType = ParamsValueTypeTuple[i].Item2;

                if (pInputType != null)
                {
                    if(pInfoType != pInputType)
                    {
                        LogHelper.PrintError("Input type is not consistent!");
                        return null;
                    }
                }

                var t1 = TypeHelper.ChangeStringToType(ParamsValueTypeTuple[i].Item1, pInfoType); // 这里需要string能转换为pInfoType
                realParams.Add(t1);
            }

            if(HasDefaultValue)
            {
                for (int j = ParamsValueTypeTuple.Count(); j < paramInfos.Count(); j++)
                {
                    var temp = paramInfos[j].DefaultValue;
                    realParams.Add(temp);
                }
            }

            var result = TheMethodInfo.Invoke(null, realParams.ToArray());
            return result;
        }

        /// <summary>
        /// Todo: 处理泛型变量等
        /// </summary>
        /// <param name="uerInputArgs"></param>
        /// <returns></returns>
        public static MyMethod ParseFromArgs(string assemblyName, string namespaceName, string[] uerInputArgs)
        {
            string helpTip = "Usage: \r\n\tClassName.Method<T1,T2> param1<T> param2<T> ...\r\n\tMethod generic type is needed when is generic method, param generic type is needed when the method name has other overload method with same name.";
            string[] items = uerInputArgs[0].Split('.');
            if (items.Count() != 2)
            {
                Console.WriteLine(helpTip);
                return null;
            }

            string curClassName = items[0];
            string curMethodNameWithT = items[1];
            string curMethodName = string.Empty;
            List<Type> curMethodGenericTypeList = new List<Type>();

            string classFullName = string.Format("{0}.{1}", namespaceName, curClassName);
            Type curClass = Assembly.Load(assemblyName).GetType(classFullName);
            if (curClass == null)
            {
                Console.WriteLine(helpTip);
                return null;
            }

            MethodInfo[] allStaticMethod = curClass.GetMethods(BindingFlags.Static | BindingFlags.Public);
            Match match = Regex.Match(curMethodNameWithT, @"(.*)<(.*)>", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                curMethodName = match.Groups[1].Value;
                string methodTypeStr = match.Groups[2].Value;
                List<string> typeItems = methodTypeStr.Split(',').Select(s => s.Trim()).ToList();
                foreach (string typeStr in typeItems)
                {
                    Type temp = TypeHelper.GetTypeFromStr(typeStr);
                    curMethodGenericTypeList.Add(temp);
                }
            }
            else
            {
                curMethodName = curMethodNameWithT;
            }

            List<MethodInfo> methodInfoList = allStaticMethod.Where(it => it.Name == curMethodName).ToList();
            List<MyMethod> meetNeedMyMethods = new List<MyMethod>();

            if (methodInfoList.Count == 0)
            {
                Console.WriteLine(helpTip);
                return null;
            }

            for (int i = 0; i < methodInfoList.Count(); i++ )
            {
                MyMethod myMethod = new MyMethod();
                myMethod.AssemblyName = assemblyName;
                myMethod.NamespaceName = namespaceName;

                MethodInfo currentMethodInfo = methodInfoList[i];
                List<string> inputParamsStr = new List<string>();
                if (currentMethodInfo.IsGenericMethod)
                {
                    if(curMethodGenericTypeList.Count() == 0)
                    {
                        continue;
                    }

                    Type[] genericTypes = currentMethodInfo.GetGenericArguments();
                    if (curMethodGenericTypeList.Count() != (genericTypes.Count()))
                    {
                        continue;
                    }

                    myMethod.GenericMethodTypes = curMethodGenericTypeList;
                    currentMethodInfo = currentMethodInfo.MakeGenericMethod(curMethodGenericTypeList.ToArray());
                }

                inputParamsStr = uerInputArgs.Skip(1).ToList();

                Dictionary<int, Type> paramIndexValueTypeDict = new Dictionary<int, Type>();   // 参数列表： 索引和类型
                List<string> paramValues = new List<string>();      //参数列表：参数值
                // 处理参数列表
                for (int j = 0; j < inputParamsStr.Count(); j++)
                {
                    Match matchInput = Regex.Match(inputParamsStr[j], @"(.*)<(.*)>", RegexOptions.IgnoreCase);
                    if (matchInput.Success)
                    {
                        string paramValue = matchInput.Groups[1].Value;
                        string typeStr = matchInput.Groups[2].Value;
                        Type paramType = TypeHelper.GetTypeFromStr(typeStr);

                        paramIndexValueTypeDict.Add(j, paramType);
                        paramValues.Add(paramValue);
                    }
                    else
                    {
                        paramIndexValueTypeDict.Add(j, null);
                        paramValues.Add(inputParamsStr[j]);
                    }
                }

                ParameterInfo[] paramInfos = currentMethodInfo.GetParameters();
                if(inputParamsStr.Count() > paramInfos.Count())
                {
                    Console.WriteLine(helpTip);
                    return null;
                }

                bool hasDefaultValue = false;  //如果参数对上，为false。 如果加上缺省参数能对上，为true
                
                if (inputParamsStr.Count() < paramInfos.Count())
                {
                    hasDefaultValue = true;
                    for (int j = inputParamsStr.Count(); j < paramInfos.Count(); j++)
                    {
                        if (!paramInfos[j].HasDefaultValue)
                        {
                            hasDefaultValue = false;
                            break;
                        }
                    }

                    if (!hasDefaultValue)
                        continue;
                }

                bool isInputMatchMethod = true;
                for (int j = 0; j < inputParamsStr.Count(); j++)
                {
                    Type paramType = paramIndexValueTypeDict[j];
                    if(paramType != null && paramType.FullName!=paramInfos[j].ParameterType.FullName)
                    {
                        isInputMatchMethod = false;
                        break;
                    }
                }

                if(!isInputMatchMethod)
                {
                    continue;
                }

                for (int j = 0; j < inputParamsStr.Count(); j++)
                {
                    string paramValue = paramValues[j];
                    Type paramType = paramIndexValueTypeDict[j];
                    Tuple<string, Type> temp = new Tuple<string, Type>(paramValue, paramType);
                    myMethod.ParamsValueTypeTuple.Add(temp);
                }

                myMethod.ClassName = curClassName;
                myMethod.MethodName = curMethodName;
                myMethod.TheMethodInfo = currentMethodInfo;
                myMethod.HasDefaultValue = hasDefaultValue;
                meetNeedMyMethods.Add(myMethod);
            }

            if (meetNeedMyMethods.Count() == 0)
            {
                Console.WriteLine(helpTip);
                return null;
            }

            if (meetNeedMyMethods.Count() > 1)
            {
                Console.WriteLine("The following methods meet need, please specify the params:");
                foreach (var item in meetNeedMyMethods)
                {
                    Console.WriteLine(classFullName);
                    string temp = TypeHelper.MethodInfo2String(item.TheMethodInfo);
                    Console.Write(temp);

                    Console.WriteLine();
                }
                return null;
            }

            return meetNeedMyMethods[0];
        }
    } 



    public class Program
    {
        private static string _assemblyName;

        private static List<XmlMethodInfo> _curXmlMehodInfos = new List<XmlMethodInfo>();

        public static void ConstructCurXmlMethodInfo(string assemblyName, List<Tuple<string, string>> namespaceClassNameDict)
        {
            string docuPath = System.IO.Path.Combine(GetCurrentDir(), assemblyName + ".xml");
            if (!File.Exists(docuPath))
            {
                throw new Exception(string.Format("xml not exists: {0}", docuPath));
            }

            XmlDocument assemblyDoc = new XmlDocument();
            assemblyDoc.Load(docuPath);

            foreach(var item in namespaceClassNameDict)
            {
                string path = string.Format("M:{0}.{1}.", item.Item1, item.Item2);

                XmlNodeList xmlDocuOfMethod = assemblyDoc.SelectNodes("//member[starts-with(@name, '" + path + "')]");

                for (int i = 0; i < xmlDocuOfMethod.Count; i++)
                {
                    XmlNode tempNode = xmlDocuOfMethod.Item(i);

                    XmlMethodInfo tempInfo = GenXmlMethodInfoFromXmlNode(tempNode, item.Item1, item.Item2);

                    _curXmlMehodInfos.Add(tempInfo);
                }
            }

            
        }

        private static XmlMethodInfo GenXmlMethodInfoFromXmlNode(XmlNode tempNode, string namespaceName, string className)
        {
            XmlMethodInfo info = new XmlMethodInfo();

            Func<XmlAttribute, string> getAttr = new Func<XmlAttribute, string>(it => {
                if (it == null)
                    return "";
                else
                    return it.Value;
            });

            string prePath = string.Format("M:{0}.{1}.", namespaceName, className);
            var fullName = getAttr(tempNode.Attributes["name"]);
            if(!fullName.Contains(prePath))
            {
                throw new Exception(string.Format("xml doc failed, name={0}", prePath));
            }

            int index1 = prePath.Count(); 
            int index2 = fullName.IndexOf('(');

            // if not found, it's a paramless function, then assign index2 the end of string.
            if(index2 == -1)
            {
                index2 = fullName.Count();    
            }

            string methodName = fullName.Substring(index1, index2 - index1);

            info.MethodFullName = string.Format("M:{0}.{1}.{2}", namespaceName, className, methodName);
            info.NamespaceName = namespaceName;
            info.ClassName = className;
            info.MethodName = methodName;

            foreach (XmlNode subNode in tempNode.ChildNodes)
            {
                if (subNode.Name == "summary")
                {
                    info.Summary = subNode.InnerText;
                }
                else if(subNode.Name == "param")
                {
                    string paramName = getAttr(subNode.Attributes["name"]);
                    string paramValue = subNode.InnerText;

                    if(!info.ParamNameCommentDict.ContainsKey(paramName))
                    {
                        info.ParamNameCommentDict.Add(paramName, paramValue);
                    }
                }
                else if (subNode.Name == "typeparam")
                {
                    string typeparamName = getAttr(subNode.Attributes["name"]);
                    string typeparamValue = subNode.InnerText;

                    if (!info.TypeparamNameCommentDict.ContainsKey(typeparamName))
                    {
                        info.TypeparamNameCommentDict.Add(typeparamName, typeparamValue);
                    }
                }
                else if(subNode.Name == "returns")
                {
                    info.ReturnComment = subNode.InnerText;
                }
            }

            return info;
        }


        public static string GetCurrentDir()
        {
            return System.IO.Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
        }



        static Program()
        {
            _assemblyName = ConfigurationManager.AppSettings["AssemblyName"];

            string tempClassnames = ConfigurationManager.AppSettings["AssemblyAllowClassnames"];
            List<string> aassemblyAllowClassnames = tempClassnames.Split(';').Distinct().ToList();
            aassemblyAllowClassnames.RemoveAll(s => string.IsNullOrEmpty(s));

            List<Tuple<string, string>> namespaceClassNameDict = new List<Tuple<string, string>>();
            foreach (Type t in Assembly.Load(_assemblyName).GetTypes())
            {
                string className = t.Name;
                string namespaceName = t.Namespace;
                if (t.IsClass && t.IsPublic)
                {
                    namespaceClassNameDict.Add(new Tuple<string, string>(namespaceName, className));
                }
            }

            //判断是否有重名的Classname（不同的namespaceName）
            int DisCount = namespaceClassNameDict.Select(it=>it.Item2).Distinct().Count();
            List<string> dupClassNames = namespaceClassNameDict.Select(it=>it.Item2).GroupBy(it=>it).Where(it=>it.Count()> 1).Select(it=>it.Key).ToList();
            if (dupClassNames.Count() > 0)
            {
                string tempLine = string.Join(",", dupClassNames);
                LogHelper.PrintError("Has duplicated class name in current assembly: {0}", tempLine);
                throw new Exception("ClassName duplicated!");
            }

            namespaceClassNameDict = namespaceClassNameDict.Where(it => aassemblyAllowClassnames.Contains(it.Item2)).ToList();

            ConstructCurXmlMethodInfo(_assemblyName, namespaceClassNameDict);
        }

        static void Main(string[] args)
        {
            if(args.Count() == 0)
            {
                ShowHelp();
                return;
            }
            else if(args.Count() == 2 && args[0]=="-h")
            {
                PringOneMethodHelpInfo(args[1]);
                return;
            }
            else if(args.Count() == 2 && args[0] == "-f")
            {
                PrintFoundMethodHelpInfos(args[1]);
                return;
            }
            else if(args.Count() >= 2)
            {
                string helpTip = "Usage: \r\n\tClassName.Method<T1,T2> param1<T> param2<T> ...\r\n\tMethod generic type is needed when is generic method, param generic type is needed when the method name has other overload method with same name.";
                string[] items = args[0].Split('.');
                if (items.Count() != 2)
                {
                    Console.WriteLine(helpTip);
                    return;
                }

                string curClassName = items[0];
                string namespaceName = GetNamespeceName(curClassName);
                if(string.IsNullOrEmpty(namespaceName))
                {
                    Console.WriteLine(helpTip);
                    return;
                }

                MyMethod my = MyMethod.ParseFromArgs(_assemblyName, namespaceName, args);
                if(my!=null)
                {
                    var result = my.Execute();
                    if(result != null)
                    {
                        Console.WriteLine(result);
                    }
                }
                return;
            }
        }

        //private static void ExecuteMethod(List<string> funcionWithParams)
        //{
        //    string[] items = funcionWithParams[0].Split('.');
        //    string helpTip = "For search help with format like \"-h ClassName.Method\"";
        //    if (items.Count() != 2)
        //    {
        //        Console.WriteLine(helpTip);
        //        return;
        //    }

        //    string className = items[0];
        //    string methodName = items[1];

        //    string classFullName = "SenseTimeHelper." + className;
        //    Type curClass = Assembly.Load("SenseTimeHelper").GetType(classFullName);
        //    if (curClass == null)
        //    {
        //        Console.WriteLine(helpTip);
        //        return;
        //    }

        //    MethodInfo curMethodInfo = curClass.GetMethod(methodName);
        //    if (curMethodInfo == null)
        //    {
        //        Console.WriteLine(helpTip);
        //        return;
        //    }

        //    List<string> inputParamsStr = new List<string>();
        //    if (curMethodInfo.IsGenericMethod)
        //    {
        //        Type[] genericTypes = curMethodInfo.GetGenericArguments();
        //        if (funcionWithParams.Count() < (1 + genericTypes.Count()))
        //        {
        //            LogHelper.PrintWarning("Input params should be greater or equal than {0}", 1 + genericTypes.Count());
        //            return;
        //        }

        //        List<string> genericStrs = funcionWithParams.Skip(1).Take(genericTypes.Count()).ToList();
        //        inputParamsStr = funcionWithParams.Skip(1 + genericTypes.Count()).ToList();

        //        List<Type> genericRealTypes = new List<Type>();
        //        foreach (string typeStr in genericStrs)
        //        {
        //            Type temp = GetTypeFromStr(typeStr);
        //            genericRealTypes.Add(temp);
        //        }

        //        curMethodInfo = curMethodInfo.MakeGenericMethod(genericRealTypes.ToArray());
        //    }
        //    else
        //    {
        //        inputParamsStr = funcionWithParams.Skip(1).ToList();
        //    }

        //    ParameterInfo[] paramInfos = curMethodInfo.GetParameters();
        //    if (inputParamsStr.Count() != paramInfos.Count())
        //    {
        //        LogHelper.PrintWarning("Input params count is not {0}", paramInfos.Count());
        //        return;
        //    }
            

        //    List<object> realParams = new List<object>();

        //    for (int i = 0; i < paramInfos.Count(); i++ )
        //    {
        //        ParameterInfo pInfo = paramInfos[i];
        //        Type pInfoType = pInfo.ParameterType;
        //        var t1 = TypeHelper.ChangeStringToType(inputParamsStr[i], pInfoType); // 这里需要string能转换为pInfoType
        //        realParams.Add(t1);
        //    }

        //    var result = curMethodInfo.Invoke(null, realParams.ToArray());

        //    if(result != null)
        //    {
        //        Console.WriteLine("result:");
        //        Console.WriteLine(result.ToString());
        //    }

        //    Console.WriteLine("Execute success!");
        //}

        private static Type GetTypeFromStr(string typeStr)
        {
            string typeStrlower = typeStr.ToLower();
            Type returnType = null;
            switch(typeStrlower)
            {
                case "string":
                    returnType = typeof(String);
                    break;
                case "int":
                    returnType = typeof(int);
                    break;
                case "float":
                    returnType = typeof(float);
                    break;
                case "double":
                    returnType = typeof(double);
                    break;
                case "char":
                    returnType = typeof(char);
                    break;
                case "datetime":
                    returnType = typeof(DateTime);
                    break;
                default:
                    break;  
            }

            return returnType ;
        }

        private static void PrintFoundMethodHelpInfos(string searchMethodName)
        {
            string helpTip = "For search help with format like \"-f PartOfMethodName\"";

            var xmlMethodInfoList = GetXmlMethodInfoList_byPartMethodName(searchMethodName);
            if(xmlMethodInfoList.Count() > 0)
            {
                PrintXmlMethodInfo(xmlMethodInfoList);
            }
            else
            {
                LogHelper.PrintWarning(helpTip);
            }
            
        }

        private static void PrintXmlMethodInfo(List<XmlMethodInfo> xmlMethodInfoList)
        {
            string lineSep = "-----------------------------------------------";
            string helpTip = "For search help with format like \"-f PartOfMethodName\"";
            bool isFoundOne = false;
            foreach (var xmlMethodInfo in xmlMethodInfoList)
            {
                string namespaceName = xmlMethodInfo.NamespaceName;
                string className = xmlMethodInfo.ClassName;
                string methodName = xmlMethodInfo.GetRealMethodName();
                var xmlParamNames = xmlMethodInfo.ParamNameCommentDict.Keys.ToList();
                var xmlGenericParamNames = xmlMethodInfo.TypeparamNameCommentDict.Keys.ToList();

                string classFullName = string.Format("{0}.{1}", namespaceName, className);
                Type curClass = Assembly.Load(_assemblyName).GetType(classFullName);
                if (curClass == null)
                {
                    Console.WriteLine(helpTip);
                    return;
                }

                MethodInfo[] allStaticMethod = curClass.GetMethods(BindingFlags.Static | BindingFlags.Public);
                List<MethodInfo> curMethodInfos = allStaticMethod.Where(it => it.Name == methodName && it.IsPublic == true).ToList();

                bool isFound = false;
                foreach (var item in curMethodInfos)
                {
                    List<string> paramNames = null;
                    List<string> genericParamNames = null;

                    TypeHelper.GetMethodInfoParams(item, out paramNames, out genericParamNames);
                    if (ListHelper.CompareTwoList(xmlParamNames, paramNames) && ListHelper.CompareTwoList(xmlGenericParamNames, genericParamNames))
                    {
                        xmlMethodInfo.RelatedMethodInfo = item;
                        isFound = true;
                        break;
                    }
                }

                if (isFound)
                {
                    Console.Write(xmlMethodInfo.ToString());
                    Console.WriteLine(lineSep);
                    isFoundOne = true;
                }
            }

            if (xmlMethodInfoList.Count() <= 0 || !isFoundOne)
            {
                Console.WriteLine(helpTip);
            }
        }

        private static void PringOneMethodHelpInfo(string methodWithClassName)
        {
            string[] items = methodWithClassName.Split('.');
            string helpTip = "For search help with format like \"-h ClassName.Method\"";
            if(items.Count() != 2)
            {
                Console.WriteLine(helpTip);
                return;
            }

            string className = items[0];
            string methodName = items[1];

            var xmlMethodInfoList = GetXmlMethodInfoList_byClassMethodName(className, methodName);
            if (xmlMethodInfoList.Count() > 0)
            {
                PrintXmlMethodInfo(xmlMethodInfoList);
            }
            else
            {
                LogHelper.PrintWarning(helpTip);
            }
        }

        private static string GetNamespeceName(string className)
        {
            string namespaceName = string.Empty;

            Func<XmlMethodInfo, bool> filter = new Func<XmlMethodInfo, bool>(it =>
            {
                bool isEqual = false;
                if (it.ClassName == className)
                {
                    isEqual = true;
                }

                return isEqual;
            });

            int count = _curXmlMehodInfos.Where(filter).Count();

            if (count > 0)
            {
                var first = _curXmlMehodInfos.First(filter);
                namespaceName = first.NamespaceName;
            }

            return namespaceName;
        }

        private static string GetClassFullName(string className)
        {
            string classFullName = string.Empty;

            string namespaceName = GetNamespeceName(className);

            if(!string.IsNullOrEmpty(namespaceName))
            {
                classFullName = string.Format("{0}.{1}", namespaceName, className);
            }

            return classFullName;
        }

        private static List<XmlMethodInfo> GetXmlMethodInfoList_byPartMethodName(string partOfMethodName)
        {
            List<XmlMethodInfo> resultInfo = new List<XmlMethodInfo>();
            Func<XmlMethodInfo, bool> filter = new Func<XmlMethodInfo, bool>(it =>
            {
                bool isFound = false;
                string realMethodName = it.GetRealMethodName();
                if(realMethodName.ToLower().Contains(partOfMethodName.ToLower()))
                {
                    isFound = true;
                }

                return isFound;
            });

            int count = _curXmlMehodInfos.Where(filter).Count();

            if (count == 0)
            {
                return resultInfo;
            }
            else
            {
                resultInfo = _curXmlMehodInfos.Where(filter).ToList();
            }

            return resultInfo;
        }

        /// <summary>
        /// 得到className和methodName指定的的XmlMethodInfo list，找不到则返回null(找不到一般是methodName没有xml结构）
        /// </summary>
        /// <param name="namespaceName"></param>
        /// <param name="className"></param>
        /// <param name="methodName"></param>
        /// <returns></returns>
        private static List<XmlMethodInfo> GetXmlMethodInfoList_byClassMethodName(string className, string methodName)
        {
            List<XmlMethodInfo> resultInfo = new List<XmlMethodInfo>();

            Func<XmlMethodInfo, bool> filter = new Func<XmlMethodInfo, bool>(it =>
            {
                bool isEqual = false;
                if (it.ClassName == className && it.MethodName == methodName)
                {
                    isEqual = true;
                }
                else if (it.ClassName == className && it.GetRealMethodName() == methodName)
                {
                    isEqual = true;
                }

                return isEqual;
            });

            int count = _curXmlMehodInfos.Where(filter).Count();

            if(count == 0)
            {
                return resultInfo;
            }
            else
            {
                resultInfo = _curXmlMehodInfos.Where(filter).ToList();
            }

            return resultInfo;
        }

        private static void ShowHelp()
        {
            string lineSep = "-----------------------------------------------";
            Console.WriteLine(lineSep);

            foreach (Type t in Assembly.Load(_assemblyName).GetTypes())
            {
                string className = t.Name;
                string namespaceName = t.Namespace;
                if (t.IsClass && t.IsPublic && _curXmlMehodInfos.Exists(it=>it.ClassName == className))
                {
                    MethodInfo[] allStaticMethod = t.GetMethods(BindingFlags.Static | BindingFlags.Public);
                    var allStaticMethodOrderList = allStaticMethod.OrderBy(it => it.Name).ToList();

                    bool isHaveXmlMethod = false;
                    foreach (var meItem in allStaticMethodOrderList)
                    {
                        string methodName = meItem.Name;

                        List<string> paramNames = null;
                        List<string> genericParamNames = null;
                        TypeHelper.GetMethodInfoParams(meItem, out paramNames, out genericParamNames);

                        var infoList = GetXmlMethodInfoList_byClassMethodName(className, methodName);

                        foreach(var item in infoList)
                        {
                            var xmlParamNames = item.ParamNameCommentDict.Keys.ToList();
                            var xmlGenericParamNames = item.TypeparamNameCommentDict.Keys.ToList();

                            if (ListHelper.CompareTwoList(xmlParamNames, paramNames) && ListHelper.CompareTwoList(xmlGenericParamNames, genericParamNames))
                            {
                                Console.WriteLine("{0}.{1}", item.ClassName, item.GetRealMethodName());
                                Console.WriteLine("\t{0}", item.Summary.Trim());
                                isHaveXmlMethod = true;
                            }
                        }
                    }
                    if (isHaveXmlMethod)
                    {
                        Console.WriteLine(lineSep);
                    }

                    
                    //MethodInfo[] allStaticMethod = t.GetMethods(BindingFlags.Static | BindingFlags.Public);
                    //var allStaticMethodList = allStaticMethod.ToList().OrderBy(it => it.Name).ToList();
                    
                    //bool isHaveDesp = false;
                    //foreach (var methodInfo in allStaticMethodList)
                    //{
                    //    object[] objs = methodInfo.GetCustomAttributes(typeof(DescriptionAttribute), true);
                    //    string methodDesText = string.Empty;
                    //    if (objs.Length > 0)
                    //    {
                    //        methodDesText = ((DescriptionAttribute)objs[0]).Description;
                    //        Console.WriteLine("{0}.{1}\t{2}", className, methodInfo.Name, methodDesText);
                    //        isHaveDesp = true;
                    //    }
                    //}

                    //if(isHaveDesp)
                    //{
                    //    Console.WriteLine(lineSep);
                    //}
                }
            }
        }

       
    }
}
