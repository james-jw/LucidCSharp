# LucidCSharp 

Set of methods for compiling text to CSharp

### Methods

##### CompileTemplate
Compiles a string literal template into a c# delegate.

The returned delegate, `titleExp` in the below example, always takes the form `Func<dynamic, string>`
```c#
  var person = new Person("John", "Doe");

  var titleTemplate = "{firstName} {lastName}";
  var titleExp = LucidCSharp.CompileTemplate(titleTemplate);
  
  Assert.AreEqual("John Doe", titleExp(person));
```

##### CompileLambda
Compiles a c# string source into a c# delegate of varying description
```c#
   var lambdaString = "i => i + 20";
   Func<int, int> operation = LucidCSharp.CompileLambda<int, int>(lambdaString);

   Assert.AreEqual(25, operation(5));

   // Supports up to 5 variables
   var complexLambda = "(a, b) => a + b";
   Func<int, int, int> complexOperation = LucidCSharp.CompileLambda<int, int, int>(complexLambda);

   Assert.AreEqual(10, complexOperation(5, 5));
```

###### Support Lambdas
The second argument of `CompileLambda` accepts an emumeration of support lambdas which this lambda can call. 

###### Logging
The last possible argumnet is a callback delegate for logging compilation failures.

##### FormatLambdaString

`string FormatLambdaString(string code)`

Formats a provided lambda string:
``` c$
   var lambda = "i => { var v = i + 5; return i < 10 ? true : false; }"
   var indentedLambda = LucidCSharp.FormatLambdaString(lambda);
   
   // indentedLambda:
   // i => {
   //   v = i + 5;
   //   return i < 10 ? true : false;
   // }
```

##### CompileAssembly
Compiles a set of c# string sources and references into an `Assembly`

