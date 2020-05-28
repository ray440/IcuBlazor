# Tips and Tricks

- Handle browser and user image differences
- Setting `IcuConfig.TestDir` to symbolic link
- Create images for documentation
- Running IcuBlazor from Commandline
<br/>
<br/>
<br/>


## Handle Browser, User and Monitor Differences
- Image capture is dependant on many factors like browser, user & monitor settings.  How can we create reliable tests under such subtleties?
- Format `CompareDiv`'s testName=$"{Browser}\\\\{tname}"

## Setting `IcuConfig.TestDir` to Symbolic Link

- Needs admin access

## Create Images for Documentation

It's handy to use your test images for documentation purposes.  Usually you will want to format your images in a consistent way and place them in a certain directory. Here's an example that uses css styles to add a border around document images and saves them in a `docs` folder.

```cs
<IcuTestDiv Suite="@this" Width="510">
    <div id="holder" style="border:5px solid #eee;">
        <MyAwesomeThing Args="setup1"/>
    </div>
</IcuTestDiv>

@code {
    public async Task Make_doc_image_setup1(Checker cx) {
        :
        await CompareDiv(cx, @"docs\awesome_setup1", "#holder");
    }

}
```

## Running IcuBlazor from Commandline

Under _samples_ see `start_server.bat` & `run_tests.bat`.


