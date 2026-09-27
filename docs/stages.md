<Project Sdk="Microsoft.NET.Sdk.Razor">

    <PropertyGroup>
        <TargetFrameworks>net10.0-android;net10.0-ios;net10.0-maccatalyst</TargetFrameworks>
        <TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('windows'))">$(TargetFrameworks);net10.0-windows10.0.19041.0</TargetFrameworks>
        <OutputType>Exe</OutputType>
        <RootNamespace>GFlow</RootNamespace>
        <UseMaui>true</UseMaui>
        <SingleProject>true</SingleProject>
        <ImplicitUsings>enable</ImplicitUsings>
        <EnableDefaultCssItems>false</EnableDefaultCssItems>
        <Nullable>enable</Nullable>
        <ApplicationTitle>GFlow</ApplicationTitle>
        <ApplicationId>com.companyname.gflow</ApplicationId>
        <ApplicationDisplayVersion>1.0</ApplicationDisplayVersion>
        <ApplicationVersion>1</ApplicationVersion>
        <WindowsPackageType>None</WindowsPackageType>
        <SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios'">15.0</SupportedOSPlatformVersion>
        <SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'maccatalyst'">15.0</SupportedOSPlatformVersion>
        <SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'android'">24.0</SupportedOSPlatformVersion>
        <SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'windows'">10.0.17763.0</SupportedOSPlatformVersion>
        <TargetPlatformMinVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'windows'">10.0.17763.0</TargetPlatformMinVersion>
    </PropertyGroup>

    <ItemGroup>
        <MauiIcon Include="ResourcesAppIconappicon.svg" ForegroundFile="ResourcesAppIconappiconfg.svg" Color="#512BD4" />
        <MauiSplashScreen Include="ResourcesSplashsplash.svg" Color="#512BD4" BaseSize="128,128" />
        <MauiImage Include="ResourcesImages*" />
        <MauiImage Update="ResourcesImagesdotnet_bot.svg" BaseSize="168,208" />
        <MauiFont Include="ResourcesFonts*" />
        <MauiAsset Include="ResourcesRaw**" LogicalName="%(RecursiveDir)%(Filename)%(Extension)" />
    </ItemGroup>

    <ItemGroup>
        <PackageReference Include="Microsoft.Maui.Controls" Version="$(MauiVersion)" />
        <PackageReference Include="Microsoft.AspNetCore.Components.WebView.Maui" Version="$(MauiVersion)" />
        <PackageReference Include="Microsoft.Extensions.Logging.Debug" Version="10.0.0" />
        <PackageReference Include="MudBlazor" Version="9.10.0" />
    </ItemGroup>

    <ItemGroup>
      <ProjectReference Include="..GFlow.CoreGFlow.Core.csproj" />
      <ProjectReference Include="..GFlow.GitHubGFlow.GitHub.csproj" />
    </ItemGroup>
</Project>
