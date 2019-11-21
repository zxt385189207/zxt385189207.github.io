```csharp
// Activator.CreateInstance的CLR重定向定义
public static StackObject* CreateInstance(ILIntepreter intp, StackObject* esp, List<object> mStack, CLRMethod method, bool isNewObj)
{
    //获取泛型参数<T>的实际类型
    IType[] genericArguments = method.GenericArguments;
    if (genericArguments != null && genericArguments.Length == 1)
    {
        var t = genericArguments[0];
        if (t is ILType)//如果T是热更DLL里的类型
        {
            //通过ILRuntime的接口来创建实例
            return ILIntepreter.PushObject(esp, mStack, ((ILType)t).Instantiate());
        }
        else
            return ILIntepreter.PushObject(esp, mStack, Activator.CreateInstance(t.TypeForCLR));//通过系统反射接口创建实例
    }
    else
        throw new EntryPointNotFoundException();
}

// 要让这段代码生效，需要执行相对应的注册方法：
foreach (var i in typeof(System.Activator).GetMethods())
{
    //找到名字为CreateInstance，并且是泛型方法的方法定义
    if (i.Name == "CreateInstance" && i.IsGenericMethodDefinition)
    {
        appdomain.RegisterCLRMethodRedirection(i, CreateInstance);
    }
}
```



```csharp
// 通过CLR重定向来实现在Debug.Log调用中打印热更DLL中的调用堆栈
public unsafe static StackObject* DLog(ILIntepreter __intp, StackObject* __esp, List<object> __mStack, CLRMethod __method, bool isNewObj)
{
    ILRuntime.Runtime.Enviorment.AppDomain __domain = __intp.AppDomain;
    StackObject* ptr_of_this_method;
    //只有一个参数，所以返回指针就是当前栈指针ESP - 1
    StackObject* __ret = ILIntepreter.Minus(__esp, 1);
    //第一个参数为ESP -1， 第二个参数为ESP - 2，以此类推
    ptr_of_this_method = ILIntepreter.Minus(__esp, 1);
    //获取参数message的值
    object message = StackObject.ToObject(ptr_of_this_method, __domain, __mStack);
    //需要清理堆栈
    __intp.Free(ptr_of_this_method);
    //如果参数类型是基础类型，例如int，可以直接通过int param = ptr_of_this_method->Value获取值，
    //关于具体原理和其他基础类型如何获取，请参考ILRuntime实现原理的文档。

    //通过ILRuntime的Debug接口获取调用热更DLL的堆栈
    string stackTrace = __domain.DebugService.GetStackTrance(__intp);
    Debug.Log(string.Format("{0}\n{1}", format, stackTrace));

    return __ret;
}


// 然后在通过下面的代码注册重定向即可：
appdomain.RegisterCLRMethodRedirection(typeof(Debug).GetMethod("Log"), DLog);
```


```csharp
//appdomain.RegisterCLRMethodRedirection(mi, Log_11);
//这个只是为了演示加的，平时不要这么用，直接在InitializeILRuntime方法里面写CLR重定向注册就行了

//编写重定向方法对于刚接触ILRuntime的朋友可能比较困难，比较简单的方式是通过CLR绑定生成绑定代码，然后在这个基础上改，比如下面这个代码是从UnityEngine_Debug_Binding里面复制来改的
 //如何使用CLR绑定请看相关教程和文档
 unsafe static StackObject* Log_11(ILIntepreter __intp, StackObject* __esp, IList<object> __mStack, CLRMethod __method, bool isNewObj)
 {
     //ILRuntime的调用约定为被调用者清理堆栈，因此执行这个函数后需要将参数从堆栈清理干净，并把返回值放在栈顶，具体请看ILRuntime实现原理文档
     ILRuntime.Runtime.Enviorment.AppDomain __domain = __intp.AppDomain;
     StackObject* ptr_of_this_method;
     //这个是最后方法返回后esp栈指针的值，应该返回清理完参数并指向返回值，这里是只需要返回清理完参数的值即可
     StackObject* __ret = ILIntepreter.Minus(__esp, 1);
     //取Log方法的参数，如果有两个参数的话，第一个参数是esp - 2,第二个参数是esp -1, 因为Mono的bug，直接-2值会错误，所以要调用ILIntepreter.Minus
     ptr_of_this_method = ILIntepreter.Minus(__esp, 1);

     //这里是将栈指针上的值转换成object，如果是基础类型可直接通过ptr->Value和ptr->ValueLow访问到值，具体请看ILRuntime实现原理文档
     object message = typeof(object).CheckCLRTypes(StackObject.ToObject(ptr_of_this_method, __domain, __mStack));
     //所有非基础类型都得调用Free来释放托管堆栈
     __intp.Free(ptr_of_this_method);

     //在真实调用Debug.Log前，我们先获取DLL内的堆栈
     var stacktrace = __domain.DebugService.GetStackTrace(__intp);

     //我们在输出信息后面加上DLL堆栈
     UnityEngine.Debug.Log(message + "\n" + stacktrace);

     return __ret;
 }
```
