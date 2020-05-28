
# Startup

Like most .Net services IcuBlazor needs to be configured.

- IcuConfig
- AddIcuBlazor()
- UseIcuServer()

## IcuConfig

IcuConfig defines the global system parameters for IcuBlazor.

- **`Name`** (Default: "All Tests")

    Name for the top Test Suite.

- **`IcuServer`** 

    IcuBlazor server url. `IcuServer` is only needed for standalone Client Side Blazor apps.

- **`WWWroot`**

    Full path of www root dir (e.g. "c:\\myproj\\wwwroot\\"). `WWWroot` is only needed for standalone Client Side Blazor apps.

- **`TestDir`** (Default: "icu_data") 

    Directory under `WWWroot` where test data files are stored.  
    - symbolic links

- **`EnableServerTests`** (Default: true)

    A local IcuBlazor server is required from some tests (e.g. `Checker.Log` & `CompareDiv`). But in some scenarios a local server is problematic (e.g. running tests on a non-localhost site). Setting `EnableServerTests=false` will effectively disable the IcuBlazor server.
    

- **`Interactive`** (Default: false) 

    Pause execution and show verify dialog when a verify test fails.  You can change this value at run time with the IcuBlazor Settings UI.

- **`Filter`** (Default: "") 

    Only run tests methods whose name contains a string. If `Filter=""` then all tests are run.  You can change this value at run time with the IcuBlazor Settings UI.

- **`StopOnFirstFailure`** (Default: false)

    Stop test execution when the first check fails otherwise IcuBlazor will continue running tests.  You can change this value at run time with the IcuBlazor Settings UI.


## AddIcuBlazor

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


## UseIcuServer

```cs
public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    ...
    app.UseIcuServer(env.WebRootPath);
    ...
}
```

# Test Setup

## IcuTestSuite
- AddTests()
    - Add Suite
    - Add Test
    - Add TestTask
- OnSuiteStart
- OnSuiteEnd




### IcuTestSuite.AddTests
    - AddDefaultTests
    - can override to sort tests, etc...

    /// Start a new Test Suite list definition.
    public void Suite(string path) {

    /// Add a test to the current Suite.
    public void Test(string testName, Action<Checker> testf) {

    /// Add an async test to the current Suite.
    public void TestTask(string testName, Func<Checker,Task> testf) {

    /// Automatically add tests using Reflection.
    public virtual void AddTests() {
        ss.AddDefaultTests(this);
    }

### IcuTestSuite.hooks...
    /// This method is called before the Suite starts.
    /// Override this method to add any test initialization code.
    public virtual void OnSuiteStart() {
    }

    /// This method is called after the Suite ends execution.
    /// Override this method to add any test cleanup code.
    public virtual void OnSuiteEnd() {
    }

<!--
    /// Trigger exection of IcuTestSuite & waits until rendering is done.  
    /// Should be used sparingly.
    public async Task<bool> RenderNow() { 
-->


## IcuTestDiv
- Width

## IcuTestViewer
- Width


# Test Checks

IcuBlazor supports traditional testing where assertions returned true/false/exception.  But what if those assertions could also produce Blazor components?

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
  
## Basic Checks

### `True(bool state, string message)`

Check that `state` is true. `message` is a description of test.

### `False(bool state, string message)`

Check that `state` is false. `message` is a description of test.

### `Fail(string message)`

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

### `Equal<T>(T expected, T actual, string message)`

Check if two primitive values (expected & actual) are equal.

### `NotEqual<T>(T expected, T actual, string message)`

Check if two primitive values (expected & actual) are NOT equal.

### `Skip(string message)`

Skip a test but display a warning message in the test viewer.

## Visual Checks

### `Text(string expect, string result, string message, int limit=800)`

Check that two strings are the same.  An error is raised of the strings are larger than the `limit`.

### `Log(string logName, string result, string message, int limit=3000)`

Check that a string is the same as a the last saved string.  The last string is saved under `"wwwwroot\{IcuConfig.TestDir}"` in a file called `"{logName}.txt"` An error is raised of the strings are larger than the `limit`.

### `CompareDiv(Checker check, string testName, string selector="#icu-test-div")`

Take a snapshot of a div and compare with previous snapshot. The previous snapshot is saved under `"wwwwroot\{IcuConfig.TestDir}"` in a file called `"{testName}.png"`.  The testName should be unique within it's `IcuTestSuite`

```cs
await CompareDiv(cx, @"init");
await CompareDiv(cx, @"docs\awesome_setup1", "#holder");
await CompareDiv(cx, @"Firefox\billg\login-message", "#login .msg");
```



# UI Automation

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

## Search for Elements

### `Find(string selector, string withText="")`

Find a single element that matches `selector`.  Use `withText` to only return elements that contain `withText`.

Examples: 
```cs
var pwdInput = await UI.Find("#login .password");
var button = await UI.Find("button", "Cancel");
```

### `FindAll(string selector, string withText="")`

Find all html elements that match `selector`.  Use `withText` to only return elements that contain `withText`.

Examples: 
```cs
var inputs = await UI.FindAll("#login > *");
var buttons = await UI.FindAll("button");
```

### `WaitForElement(string selector, string withText="", int timeout=5000, int interval=200)`

Wait for a single element to be added to DOM. Use `selector` and `withText` to specify which element to return.
```cs
public async Task Got_any_questions(Checker cx) {
    await click_ok_button(); 
    // wait for results to appear
    var e = await UI.WaitForElement(".hdr-title", "question");
    await CompareDiv(cx, "got_question");
}
```

## Manipulate Elements

### `HtmlContent(ElemRef e)`

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

### `TextContent(ElemRef e)`

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

### `CompareDiv(Checker check, string testName, string selector="#icu-test-div")`

- maybe see checker.CheckDiv

### `Click(ElemRef e)`

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
### `SetValue(ElemRef e, string v)`
Set html element.value = v. Typically used to enter text in input fields.
```cs
var pwd = await UI.Find("#password");
await UI.SetValue(pwd, "MrSnuggles");
```

### `Eval(string code)`
Execute javascript code.  This is a catch-all for JS automation.
```cs
// find password field and set it.
var code = "document.getElementById('password').value='MrSnuggles'";
var res = UI.EvalJson(code);
```
### `EvalJson<T>(string code)`
Execute javascript code and return result as JSON.  This is a catch-all for JS automation.


## Wait

Most UI systems are deeply asynchronous meaning that any UI automation API can use some routines to wait and synchronize tests.  IcuBlazor offers the following helpers. Generally, methods that start with `Wait.For` will wait until a `getter()` function returns a non-null value. `Wait.Until` methods wait until a function `isDone()` returns true.  The wait mechanism is a straightforward polling algorithm. It will poll every `interval` milliseconds and stop with a `TimeoutException` after `timeout` milliseconds.


### `ForAsync<T>(Func<Task<T>> getter, int timeout=5000, int interval=200)`

Wait by polling until an async `getter()` returns a non-null value.

### `UntilAsync(Func<Task<bool>> isDone, int timeout=5000, int interval=200)`

Wait until an async `isDone()` is true.

### `For<T>(Func<T> getter, int timeout=5000, int interval=200)`
Wait by polling until `getter()` returns a non-null value.

### `Until(Func<bool> isDone, int timeout=5000, int interval=200)`
Wait until `isDone()` is true.

