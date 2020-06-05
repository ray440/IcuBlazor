

<h1>API Documentation </h1>

<!-- TOC depthfrom:1 depthto:4 -->

- [1. Startup](#1-startup)
    - [1.1. `IcuConfig`](#11-icuconfig)
        - [1.1.1. System Parameters](#111-system-parameters)
        - [1.1.2. Runtime Parameters](#112-runtime-parameters)
    - [1.2. `AddIcuBlazor(IcuConfig cfg)`](#12-addicublazoricuconfig-cfg)
    - [1.3. `UseIcuServer(string webroot)`](#13-useicuserverstring-webroot)
- [2. Test Setup](#2-test-setup)
    - [2.1. `<IcuTestSuite/>`](#21-icutestsuite)
        - [2.1.1. `AddTests()`](#211-addtests)
        - [2.1.2. `Suite(string path)`](#212-suitestring-path)
        - [2.1.3. `Test(string testName, Action<Checker> testf)`](#213-teststring-testname-actionchecker-testf)
        - [2.1.4. `TestTask(string testName, Func<Checker,Task> testf)`](#214-testtaskstring-testname-funccheckertask-testf)
        - [2.1.5. `OnSuiteStart()` and `OnSuiteEnd()`](#215-onsuitestart-and-onsuiteend)
    - [2.2. `<IcuTestDiv/>`](#22-icutestdiv)
    - [2.3. `<IcuTestViewer/>`](#23-icutestviewer)
- [3. Test Checks](#3-test-checks)
    - [3.1. Basic Checks](#31-basic-checks)
        - [3.1.1. `True(bool state, string message)`](#311-truebool-state-string-message)
        - [3.1.2. `False(bool state, string message)`](#312-falsebool-state-string-message)
        - [3.1.3. `Fail(string message)`](#313-failstring-message)
        - [3.1.4. `Equal<T>(T expected, T actual, string message)`](#314-equaltt-expected-t-actual-string-message)
        - [3.1.5. `NotEqual<T>(T expected, T actual, string message)`](#315-notequaltt-expected-t-actual-string-message)
        - [3.1.6. `Skip(string message)`](#316-skipstring-message)
    - [3.2. Visual Checks](#32-visual-checks)
        - [3.2.1. `Text(string expect, string result, string message, int limit=800)`](#321-textstring-expect-string-result-string-message-int-limit800)
        - [3.2.2. `Log(string logName, string result, string message, int limit=3000)`](#322-logstring-logname-string-result-string-message-int-limit3000)
        - [3.2.3. `CompareDiv(Checker check, string testName, string selector="#icu-test-div")`](#323-comparedivchecker-check-string-testname-string-selectoricu-test-div)
- [4. UI Automation](#4-ui-automation)
    - [4.1. Search for Elements](#41-search-for-elements)
        - [4.1.1. `Find(string selector, string withText="")`](#411-findstring-selector-string-withtext)
        - [4.1.2. `FindAll(string selector, string withText="")`](#412-findallstring-selector-string-withtext)
        - [4.1.3. `WaitForElement(string selector, string withText="", int timeout=5000, int interval=200)`](#413-waitforelementstring-selector-string-withtext-int-timeout5000-int-interval200)
    - [4.2. Manipulate Elements](#42-manipulate-elements)
        - [4.2.1. `HtmlContent(ElemRef e)`](#421-htmlcontentelemref-e)
        - [4.2.2. `TextContent(ElemRef e)`](#422-textcontentelemref-e)
        - [4.2.3. `CompareDiv(Checker check, string testName, string selector="#icu-test-div")`](#423-comparedivchecker-check-string-testname-string-selectoricu-test-div)
        - [4.2.4. `Click(ElemRef e)`](#424-clickelemref-e)
        - [4.2.5. `SetValue(ElemRef e, string v)`](#425-setvalueelemref-e-string-v)
        - [4.2.6. `Eval(string code)`](#426-evalstring-code)
        - [4.2.7. `EvalJson<T>(string code)`](#427-evaljsontstring-code)
    - [4.3. Wait](#43-wait)
        - [4.3.1. `ForAsync<T>(Func<Task<T>> getter, int timeout=5000, int interval=200)`](#431-forasynctfunctaskt-getter-int-timeout5000-int-interval200)
        - [4.3.2. `UntilAsync(Func<Task<bool>> isDone, int timeout=5000, int interval=200)`](#432-untilasyncfunctaskbool-isdone-int-timeout5000-int-interval200)
        - [4.3.3. `For<T>(Func<T> getter, int timeout=5000, int interval=200)`](#433-fortfunct-getter-int-timeout5000-int-interval200)
        - [4.3.4. `Until(Func<bool> isDone, int timeout=5000, int interval=200)`](#434-untilfuncbool-isdone-int-timeout5000-int-interval200)

<!-- /TOC -->



# 1. Startup

## 1.1. `IcuConfig`

### 1.1.1. System Parameters
IcuConfig defines the global system properties for IcuBlazor.  These values are defined at system startup and typically do not change.

- **`Name`** (Default: "All Tests")
    Name for the top Test Suite.
    <br/>

- **`IcuServer`** 
    IcuBlazor server url. `IcuServer` is only needed for standalone Client Side Blazor apps.
    <br/>

- **`WWWroot`**
    Full path of www root dir (e.g. "c:\\myproj\\wwwroot\\"). You only need to define `WWWroot` for standalone Client Side Blazor apps.
    <br/>

- **`TestDir`** (Default: "icu_data") 
    Directory under `WWWroot` where test data files are stored.  
    <br/>

- **`EnableServerTests`** (Default: true)
    A local IcuBlazor server is required from some tests (e.g. `Checker.Log` & `CompareDiv`). But in some scenarios a local server is problematic (e.g. running tests on a non-localhost site). Setting `EnableServerTests=false` will effectively disable the IcuBlazor server.
    <br/>
    
- **`Verosity`** (Default: LogLevel.info)
    Sets the logging level for IcuBlazor output.
    <br/>
    
### 1.1.2. Runtime Parameters
IcuConfig also defines some runtime parameters. You can change them at run time with the IcuBlazor Settings UI.  These values persist in the browsers LocalStorage.  

- **`Interactive`** (Default: false) 
    Pause execution and show verify dialog when a visual test fails.  
    <br/>

- **`Filter`** (Default: "") 
    Only run test methods whose name contains `Filter`. If `Filter=""` then all tests are run.
    <br/>

- **`StopOnFirstFailure`** (Default: false)
    Stop test execution when the first check fails otherwise IcuBlazor will continue running tests.
    <br/>


## 1.2. `AddIcuBlazor(IcuConfig cfg)`

To configure IcuBlazor servers you will typically need something like this:

```cs
public void ConfigureServices(IServiceCollection services)
{
    ...
    services.AddIcuBlazor(new IcuConfig {
        TestDir = @"my_test_data",
        Verbosity = LogLevel.Error,
    });
    ...
}
```

For standalone Client-Side Blazor apps (CSBs):
```cs
public static async Task Main(string[] args)
{
    ...
    builder.Services.AddIcuBlazor(new IcuConfig {
        IcuServer = "https://localhost:44322/",
        WWWroot = "C:\\change-to-actual-path\\CSB\\wwwroot\\",
        //EnableServerTests = false,
    });
    ...
}
```


## 1.3. `UseIcuServer(string webroot)`

The following will configure IcuBlazor servers.

```cs
public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    ...
    app.UseIcuServer(env.WebRootPath);
    ...
}
```

# 2. Test Setup

## 2.1. `<IcuTestSuite/>`

`<IcuTestSuite/>` is basically a container for your test methods.

```cs
@page "/MyTests/TestExample"
@inherits IcuBlazor.IcuTestSuite

@code {
    public void SimpleChecks(Checker check)
    {
        check.True(2 < 3, "a true test");
        check.Equal(6*9, 42, "What's the question?");
    }
    void Test_ShouldFail(Checker check)
    {
        var (a, b) = ("Hello", "World");
        check.NotEqual(a + b, "Hello World", "Not quite right");
    }
}
```

### 2.1.1. `AddTests()`
By default IcuBlazor automatically adds tests within a suite.  It uses Reflection to find tests and executes them in the order they were defined.  You can change this default by overriding `IcuTestSuite.AddTests()`.  For example:

```cs
public override void AddTests()
{
    //base.AddTests();  // default: adds tests using Reflection

    Suite("My Test Ordering");
    Test("Should Pass", SimpleChecks);
    Test("Should Fail", ShouldFail);
    TestTask("Async task", TestAsyncMethod);
    Test("Anonymous Test", (c) => {
        c.True(10 > 2, "Anonymous function works");
    });
    TestTask("Async Anonymous test", async (c) => {
        await Task.Delay(200);
        c.True(true, "Inline async tests");
    });
}
```

### 2.1.2. `Suite(string path)`
Start a new Test Suite list definition.

### 2.1.3. `Test(string testName, Action<Checker> testf)`
Add a test to the current Suite.

### 2.1.4. `TestTask(string testName, Func<Checker,Task> testf)`
Add an async test to the current Suite.


### 2.1.5. `OnSuiteStart()` and `OnSuiteEnd()`

IcuBlazor provides hooks so that you can initialize and cleanup test suites.

```cs
    public override void OnSuiteStart() {
        base.OnSuiteStart();
        emailer = getEmailService();
    }
    public override void OnSuiteEnd() {
        emailer.Dispose();
        base.OnSuiteEnd();
    }
```


## 2.2. `<IcuTestDiv/>`

`<IcuTestDiv/>` is just a div where you can arrange your UI for testing. As a div it can contain any html content and can have any css style.  By default it has the id `#icu-test-div` which corresponds to the default selector in `CompareDiv()`.  For testing consistency you must provide a `Width` argument.

```html
<IcuTestDiv Suite="@this" Width="300" style="border:5px solid #ccc;">
    <p>Any Html or Blazor content!</p>
    <WeatherCounter @ref="myComponent"/>
</IcuTestDiv>
```

## 2.3. `<IcuTestViewer/>`

`<IcuTestViewer/>` is a container for a list of `IcuTestSuites`.

For testing consistency you must provide a `Width` argument.

```html
@inject IcuConfig icu

<IcuTestViewer Width="1000">
    <TestExample />
    <TestSystemInfo />
    @if (icu.EnableServerTests) {
        <TestDiffText />
        <TestDiffImage />
    }    
</IcuTestViewer>

```

# 3. Test Checks

IcuBlazor supports traditional testing where assertions return true, false or an exception.  But what if those assertions could also produce Blazor components? What could we do? Here we document the traditional basic checks as well as the new visual checks.

- Basic Checks
    - True
    - False
    - Fail
    - Equal
    - NotEqual
    - Skip

- Visual Checks
    - Text
    - Log
    - CompareDiv
  
## 3.1. Basic Checks

### 3.1.1. `True(bool state, string message)`

Check that `state` is true. `message` is a description of test.

### 3.1.2. `False(bool state, string message)`

Check that `state` is false. `message` is a description of test.

### 3.1.3. `Fail(string message)`

Just declare a failed test.  For example:
```cs
try {
    var x = 1;
    var v = 10/(1-x);
    cx.Fail("Should throw div by zero exception.");
} catch (Exception) {
    // Nothing to see here. Carry on.
}
```

### 3.1.4. `Equal<T>(T expected, T actual, string message)`

Check if two primitive values (expected & actual) are equal.

### 3.1.5. `NotEqual<T>(T expected, T actual, string message)`

Check if two primitive values (expected & actual) are NOT equal.

### 3.1.6. `Skip(string message)`

Skip a test but display a warning message in the test viewer.

## 3.2. Visual Checks

### 3.2.1. `Text(string expect, string result, string message, int limit=800)`

Check that two strings are the same.  An error is raised of the strings are larger than the `limit`.

### 3.2.2. `Log(string logName, string result, string message, int limit=3000)`

Check that a string is the same as a the last saved string.  The last string is saved under `"wwwwroot\{IcuConfig.TestDir}"` in a file called `"{logName}.txt"` An error is raised of the strings are larger than the `limit`.

### 3.2.3. `CompareDiv(Checker check, string testName, string selector="#icu-test-div")`

Take a snapshot of a div and compare with previous snapshot. The previous snapshot is saved under `"wwwwroot\{IcuConfig.TestDir}"` in a file called `"{testName}.png"`.  The testName should be unique within it's `IcuTestSuite`

```cs
await CompareDiv(cx, @"init");
await CompareDiv(cx, @"docs\awesome_setup1", "#holder");
await CompareDiv(cx, @"Firefox\billg\login-message", "#login .msg");
```



# 4. UI Automation

UI Automation involves searching for and manipulating HTML elements.  And since UIs are deeply asynchronous, some waiting routines are helpful.

- Search For elements    
    - FindAll(string selector, string withText = "")
    - Find(string selector, string withText = "")
    - WaitForElement(string selector, string withText = "", int timeout=5000, int interval=200)

- Manipulate elements
    - HtmlContent(ElemRef e)
    - TextContent(ElemRef e)
    - Click(ElemRef e)
    - SetValue(ElemRef e, string v)
    - Eval(string code)
    - EvalJson<T>(string code)

- Waiting helpers
    - ForAsync<T>(Func<Task<T>> getter, int timeout=5000, int interval=200)
    - UntilAsync(Func<Task<bool>> isDone, int timeout=5000, int interval=200)
    - For<T>(Func<T> getter, int timeout=5000, int interval=200)
    - Until(Func<bool> isDone, int timeout=5000, int interval=200)

## 4.1. Search for Elements

### 4.1.1. `Find(string selector, string withText="")`

Find a single element that matches `selector`.  Use `withText` to only return elements that contain `withText`.

Examples: 
```cs
var pwdInput = await UI.Find("#login .password");
var button = await UI.Find("button", "Cancel");
```

### 4.1.2. `FindAll(string selector, string withText="")`

Find all html elements that match `selector`.  Use `withText` to only return elements that contain `withText`.

Examples: 
```cs
var inputs = await UI.FindAll("#login > *");
var buttons = await UI.FindAll("button");
```

### 4.1.3. `WaitForElement(string selector, string withText="", int timeout=5000, int interval=200)`

Wait for a single element to be added to DOM. Use `selector` and `withText` to specify which element to return.
```cs
public async Task Got_any_questions(Checker cx) {
    await click_ok_button(); 
    // wait for results to appear
    var e = await UI.WaitForElement(".hdr-title", "question");
    await CompareDiv(cx, "got_question");
}
```

## 4.2. Manipulate Elements

### 4.2.1. `HtmlContent(ElemRef e)`

Get the html content of an html element.
```cs
public async Task Got_any_questions(Checker cx) {
    await click_ok_button(); 
    // wait for results to appear
    var dv = await UI.WaitForElement(".hdr-title", "question");
    var html = await UI.HtmlContent(dv);
    var text = await UI.TextContent(dv);
    cx.Log("MyComp_html", html, "Test div html content");
    cx.Log("MyComp_text", text, "Test div text content");
}
```

### 4.2.2. `TextContent(ElemRef e)`

Get the text content of an html element 

```cs
public async Task Got_any_questions(Checker cx) {
    await click_ok_button(); 
    // wait for results to appear
    var dv = await UI.WaitForElement(".hdr-title", "question");
    var html = await UI.HtmlContent(dv);
    var text = await UI.TextContent(dv);
    cx.Log("MyComp_html", html, "Test div html content");
    cx.Log("MyComp_text", text, "Test div text content");
}
```

### 4.2.3. `CompareDiv(Checker check, string testName, string selector="#icu-test-div")`

- maybe see checker.CheckDiv

### 4.2.4. `Click(ElemRef e)`

Send a click event to an html element.
```cs
public async Task Test_Counter_UI(Checker cx)
{
    var button = await UI.Find("button", "Hotter");
    await UI.Click(button);
    await UI.Click(button);
    await CompareDiv(cx, "click_twice");
}
```

### 4.2.5. `SetValue(ElemRef e, string v)`
Set html element.value = v. Typically used to enter text in input fields.
```cs
var pwd = await UI.Find("#password");
await UI.SetValue(pwd, "MrSnuggles");
```

### 4.2.6. `Eval(string code)`
Execute javascript code.  This is a catch-all for JS automation.
```cs
// find password field and set it.
var code = "document.getElementById('password').value='MrSnuggles'";
var res = UI.EvalJson(code);
```

### 4.2.7. `EvalJson<T>(string code)`
Execute javascript code and return result as JSON.  This is a catch-all for JS automation.


## 4.3. Wait

Most UI systems are deeply asynchronous meaning that any UI automation API can use some routines to wait and synchronize tests.  IcuBlazor offers the following helpers. Generally, methods that start with `Wait.For` will wait until a `getter()` function returns a non-null value. `Wait.Until` methods wait until a function `isDone()` returns true.  The wait mechanism is a straightforward polling algorithm. It will poll every `interval` milliseconds and stop with a `TimeoutException` after `timeout` milliseconds.


### 4.3.1. `ForAsync<T>(Func<Task<T>> getter, int timeout=5000, int interval=200)`

Wait by polling until an async `getter()` returns a non-null value.

### 4.3.2. `UntilAsync(Func<Task<bool>> isDone, int timeout=5000, int interval=200)`

Wait until an async `isDone()` is true.

### 4.3.3. `For<T>(Func<T> getter, int timeout=5000, int interval=200)`
Wait by polling until `getter()` returns a non-null value.

### 4.3.4. `Until(Func<bool> isDone, int timeout=5000, int interval=200)`
Wait until `isDone()` is true.

