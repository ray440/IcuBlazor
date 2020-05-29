<!-- TOC -->

- [1. Tips and Tricks](#1-tips-and-tricks)
    - [1.1. Handle Subtle Image Differences](#11-handle-subtle-image-differences)
    - [1.2. Setting `IcuConfig.TestDir` to Symbolic Link](#12-setting-icuconfigtestdir-to-symbolic-link)
    - [1.3. Create Images for Documentation](#13-create-images-for-documentation)
    - [1.4. Running IcuBlazor from Commandline](#14-running-icublazor-from-commandline)

<!-- /TOC -->
![](http://IcuBlazor/Logo?api)

# 1. Tips and Tricks

## 1.1. Handle Subtle Image Differences

Captured test images can vary a lot depending on browser, user & monitor settings.  How can we create reliable tests under such subtleties?

1. One answer is to crush all diversity!  Make sure your test environemt is the same whenever it is run (i.e. on the same browser, user & monitor settings)

1. Another solution is to create separate test images for each environment setting. For example, to handle browser differences save images in a "Chrome\\" or "Firefox\\" folder.  Just format CompareDiv()'s testName: `testName=Env.Browser+"\\"+testName`.

## 1.2. Setting `IcuConfig.TestDir` to Symbolic Link

To view test images your website those images must to be under wwwroot.  But you may want to store them elsewhere.  You can create a symbolic link (e.g. `mklink /D link target`) and set `IcuConfig.TestDir=link` to a symbolic link and the files can be redirect anywhere you want.  Note: creating a symbolic link (e.g. `mklink /D link target`) will require admin privledges.


## 1.3. Create Images for Documentation

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

## 1.4. Running IcuBlazor from Commandline

Under _samples_ see `start_server.bat` & `run_tests.bat`.


