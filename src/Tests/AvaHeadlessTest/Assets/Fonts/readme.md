1. Save fonts in this diretory

2. Insert the code below:
```xml
<Application.Resources>
  <FontFamily x:Key="Arial">avares://AvaHeadlessTest/Assets/Fonts#Arial</FontFamily>
</Application.Resources>
```

3. Set `FontFamily` in xaml:

```xml
<avalonia:AvaRenderingControl FontFamily="{StaticResource Arial}">
  ...
</avalonia:AvaRenderingControl>
```