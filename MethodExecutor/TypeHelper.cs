using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MethodExecutor
{
    public static class TypeHelper
    {
        public static Type GetTypeFromStr(string typeStr)
        {
            string typeStrlower = typeStr.ToLower();
            Type returnType = null;
            switch (typeStrlower)
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
                case "single":
                    returnType = typeof(Single);
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

            return returnType;
        }

        public static void GetMethodInfoParams(MethodInfo curMethodInfo, out List<string> paramNames, out List<string> genericparamNames)
        {
            paramNames = new List<string>();
            genericparamNames = new List<string>();

            foreach(var item in curMethodInfo.GetParameters())
            {
                paramNames.Add(item.Name);
            }

            if(curMethodInfo.IsGenericMethod)
            {
                string methodInterfaceLine = curMethodInfo.ToString();  //returnType MethodName[T1, T2](T1, T2)
                int index1 = methodInterfaceLine.IndexOf(curMethodInfo.Name) + curMethodInfo.Name.Count() + 1;
                int index2 = methodInterfaceLine.IndexOf('(') - 1;

                if (index1 > 0 && index2 > 0)
                {
                    string genericParamLine = methodInterfaceLine.Substring(index1, index2 - index1);
                    genericparamNames = genericParamLine.Split(',').ToList();
                    genericparamNames.RemoveAll(s => string.IsNullOrEmpty(s));
                }
            }
        }

        public static string MethodInfo2String(MethodInfo curMethodInfo)
        {
            StringBuilder sb = new StringBuilder();

            ////得到[Description("")]显示的内容
            //object[] objs = curMethodInfo.GetCustomAttributes(typeof(DescriptionAttribute), true);
            //string methodDesText = string.Empty;
            //if (objs.Length > 0)
            //{
            //    methodDesText = ((DescriptionAttribute)objs[0]).Description;
            //}
            //sb.AppendLine(string.Format("Description:{0}", methodDesText));

            ParameterInfo[] paramInfos = curMethodInfo.GetParameters();
            string paramsLine = string.Empty;
            for (int i = 0; i < paramInfos.Count(); i++)
            {
                paramsLine += paramInfos[i].ToString();

                if(paramInfos[i].HasDefaultValue)
                {
                    if (paramInfos[i].DefaultValue != null)
                    {
                        if (paramInfos[i].DefaultValue is string)
                        {
                            paramsLine += string.Format("=\"{0}\"", paramInfos[i].DefaultValue);
                        }
                        else if (paramInfos[i].DefaultValue is char)
                        {
                            paramsLine += string.Format("='{0}'", ConvertConvertCharToString((char)paramInfos[i].DefaultValue));
                        }
                        else
                        {
                            paramsLine += string.Format("={0}", paramInfos[i].DefaultValue.ToString());
                        }
                    }
                    else
                    {
                        paramsLine += string.Format("={0}", "null");
                    }
                    
                }

                if (i < (paramInfos.Count() - 1))
                {
                    paramsLine += ", ";
                }
            }

            string methodGenericStr = string.Empty;
            if(curMethodInfo.IsGenericMethod)
            {
                string methodInterfaceLine = curMethodInfo.ToString();  //returnType MethodName[T1, T2](T1, T2)
                int index1 = methodInterfaceLine.IndexOf(curMethodInfo.Name) + curMethodInfo.Name.Count();
                int index2 = methodInterfaceLine.IndexOf('(');
            
                if(index1 > 0 && index2 > 0)
                {
                    methodGenericStr = methodInterfaceLine.Substring(index1, index2 - index1);
                    methodGenericStr = methodGenericStr.Replace('[', '<');
                    methodGenericStr = methodGenericStr.Replace(']', '>');
                }
            }

            string functionFullLine = string.Format("{0}{1}{2}({3})", curMethodInfo.ReturnParameter.ToString(), curMethodInfo.Name, methodGenericStr, paramsLine);

            sb.AppendLine(functionFullLine);

            return sb.ToString();
        }

        /// <summary>
        /// ChangeType can deal with Nullable types.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="conversionType"></param>
        /// <returns></returns>
        public static object ChangeStringToType(string value, Type conversionType)
        {
            // Note: This if block was taken from Convert.ChangeType as is, and is needed here since we're
            // checking properties on conversionType below.
            if (conversionType == null)
            {
                throw new ArgumentNullException("conversionType");
            } // end if

            // If it's not a nullable type, just pass through the parameters to Convert.ChangeType

            if (conversionType.IsGenericType &&
              conversionType.GetGenericTypeDefinition().Equals(typeof(Nullable<>)))
            {
                // It's a nullable type, so instead of calling Convert.ChangeType directly which would throw a
                // InvalidCastException (per http://weblogs.asp.net/pjohnson/archive/2006/02/07/437631.aspx),
                // determine what the underlying type is
                // If it's null, it won't convert to the underlying type, but that's fine since nulls don't really
                // have a type--so just return null
                // Note: We only do this check if we're converting to a nullable type, since doing it outside
                // would diverge from Convert.ChangeType's behavior, which throws an InvalidCastException if
                // value is null and conversionType is a value type.
                if (value == null)
                {
                    return null;
                } // end if

                // It's a nullable type, and not null, so that means it can be converted to its underlying type,
                // so overwrite the passed-in conversion type with this underlying type
                NullableConverter nullableConverter = new NullableConverter(conversionType);
                conversionType = nullableConverter.UnderlyingType;
            } // end if

            if (typeof(System.Enum).IsAssignableFrom(conversionType))
            {
                return Enum.Parse(conversionType, value.ToString());
            }

            if (conversionType == typeof(char))
            {
                return ConvertStringToConvertChar(value);
            }

            // Now that we've guaranteed conversionType is something Convert.ChangeType can handle (i.e. not a
            // nullable type), pass the call on to Convert.ChangeType
            return Convert.ChangeType(value, conversionType, CultureInfo.InvariantCulture);
        }

        public static T ChangeObjectToType<T>(object value)
        {
            var toType = typeof(T);

            if (value == null) return default(T);

            if (value is string)
            {
                if (toType == typeof(Guid))
                {
                    return ChangeObjectToType<T>(new Guid(Convert.ToString(value, CultureInfo.InvariantCulture)));
                }
                if ((string)value == string.Empty && toType != typeof(string))
                {
                    return ChangeObjectToType<T>(null);
                }
            }
            else
            {
                if (typeof(T) == typeof(string))
                {
                    return ChangeObjectToType<T>(Convert.ToString(value, CultureInfo.InvariantCulture));
                }
            }

            if (toType.IsGenericType &&
                toType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                toType = Nullable.GetUnderlyingType(toType); ;
            }

            bool canConvert = toType is IConvertible || (toType.IsValueType && !toType.IsEnum);
            if (canConvert)
            {
                return (T)Convert.ChangeType(value, toType, CultureInfo.InvariantCulture);
            }
            return (T)value;
        }

        /// <summary>
        /// 将转义字符转换为字符串，显示出来给看
        /// </summary>
        /// <param name="ch"></param>
        /// <returns></returns>
        private static string ConvertConvertCharToString(char ch)
        {
            string result = "";
            if(ch == '\t')
            {
                result = "\\t";
            }
            else if(ch == '\r')
            {
                result = "\\r";
            }
            else if (ch == '\n')
            {
                result = "\\n";
            }
            else
            {
                result = ch.ToString();
            }

            return result;
        }

        /// <summary>
        /// 转义字符串转换为转义字符, 如"\t"转换为char
        /// </summary>
        /// <param name="charStr"></param>
        /// <returns></returns>
        private static char ConvertStringToConvertChar(string charStr)
        {
            char temp = '\0';
            if (charStr == "\\t")
            {
                temp = char.Parse("\t");
            }
            else if (charStr == "\\r")
            {
                temp = char.Parse("\r");
            }
            else if (charStr == "\\n")
            {
                temp = char.Parse("\n");
            }
            else if (charStr == "\\v")
            {
                temp = char.Parse("\v");
            }
            else if (charStr == "\\'")
            {
                temp = char.Parse("\'");
            }
            else if (charStr == "\\\"")
            {
                temp = char.Parse("\"");
            }
            else
            {
                temp = char.Parse(charStr);
            }

            return temp;
        }
    }

    class ListHelper
    {
        public static bool CompareTwoList(List<string> list1, List<string> list2)
        {
            if (list1 == null || list2 == null)
            {
                return false;
            }

            bool isEqual = true;
            if (list1.Count() != list2.Count())
            {
                isEqual = false;
            }
            else
            {
                for (int i = 0; i < list1.Count(); i++)
                {
                    if (list1[i] != list2[i])
                    {
                        isEqual = false;
                        break;
                    }
                }
            }

            return isEqual;

        }
    }

    class LogHelper
    {
        /// <summary>
        /// Print error message.
        /// </summary>
        /// <param name="errorMessage">Error message.</param>
        public static void PrintError(string errorMessage)
        {
            ConsoleColor foreColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(errorMessage);
            Console.ForegroundColor = foreColor;
        }

        public static void PrintError(string format, params object[] args)
        {
            string errorMsg = string.Format(format, args);
            PrintError(errorMsg);
        }

        /// <summary>
        /// Print warning message.
        /// </summary>
        /// <param name="warningMessage">Warning message.</param>
        public static void PrintWarning(string warningMessage)
        {
            ConsoleColor foreColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(warningMessage);
            Console.ForegroundColor = foreColor;
        }

        public static void PrintWarning(string format, params object[] args)
        {
            string warningMsg = string.Format(format, args);
            PrintWarning(warningMsg);
        }

        /// <summary>
        /// Print normal message.
        /// </summary>
        /// <param name="infoMessage">Info message.</param>
        public static void PrintInfo(string infoMessage)
        {
            Console.WriteLine(infoMessage);
        }

        public static void PrintInfo(string format, params object[] args)
        {
            string infoMsg = string.Format(format, args);
            PrintInfo(infoMsg);
        }
    }
}
