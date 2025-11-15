```cs
// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Ui.Controls;

/// <summary>
/// Defines several predefined text styles that you can apply to some elements responsible for displaying it.
/// <para><see href="https://learn.microsoft.com/en-us/windows/apps/design/style/typography"/></para>
/// </summary>
public enum FontTypography
{
    Caption,
    Body,
    BodyStrong,
    Subtitle,
    Title,
    TitleLarge,
    Display,
}
```

```cs
// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Ui.Controls;

namespace Wpf.Ui.Extensions;

/// <summary>
/// Extension that converts the typography type enumeration to the name of the resource that represents it.
/// </summary>
public static class TextBlockFontTypographyExtensions
{
    /// <summary>
    ///  Converts the typography type enumeration to the name of the resource that represents it.
    /// </summary>
    /// <returns>Name of the resource matching the <see cref="FontTypography"/>. <see cref="ArgumentOutOfRangeException"/> otherwise.</returns>
    public static string ToResourceValue(this FontTypography typography)
    {
        return typography switch
        {
            FontTypography.Caption => "CaptionTextBlockStyle",
            FontTypography.Body => "BodyTextBlockStyle",
            FontTypography.BodyStrong => "BodyStrongTextBlockStyle",
            FontTypography.Subtitle => "SubtitleTextBlockStyle",
            FontTypography.Title => "TitleTextBlockStyle",
            FontTypography.TitleLarge => "TitleLargeTextBlockStyle",
            FontTypography.Display => "DisplayTextBlockStyle",
            _ => throw new ArgumentOutOfRangeException(nameof(typography), typography, null),
        };
    }
}
```

```xaml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <Style TargetType="TextBlock">
        <Setter Property="FontSize" Value="14" />
        <Setter Property="LineHeight" Value="20" />
        <Setter Property="FontWeight" Value="Regular" />
    </Style>

    <Style
        x:Key="CaptionTextBlockStyle"
        BasedOn="{StaticResource {x:Type TextBlock}}"
        TargetType="{x:Type TextBlock}">
        <Setter Property="FontSize" Value="12" />
        <Setter Property="LineHeight" Value="16" />
        <Setter Property="FontWeight" Value="Regular" />
    </Style>

    <Style
        x:Key="BodyTextBlockStyle"
        BasedOn="{StaticResource {x:Type TextBlock}}"
        TargetType="{x:Type TextBlock}">
        <Setter Property="FontSize" Value="14" />
        <Setter Property="LineHeight" Value="20" />
        <Setter Property="FontWeight" Value="Regular" />
    </Style>

    <Style
        x:Key="BodyStrongTextBlockStyle"
        BasedOn="{StaticResource {x:Type TextBlock}}"
        TargetType="{x:Type TextBlock}">
        <Setter Property="FontSize" Value="14" />
        <Setter Property="LineHeight" Value="20" />
        <Setter Property="FontWeight" Value="SemiBold" />
    </Style>

    <Style
        x:Key="SubtitleTextBlockStyle"
        BasedOn="{StaticResource {x:Type TextBlock}}"
        TargetType="{x:Type TextBlock}">
        <Setter Property="FontSize" Value="20" />
        <Setter Property="LineHeight" Value="28" />
        <Setter Property="FontWeight" Value="SemiBold" />
    </Style>

    <Style
        x:Key="TitleTextBlockStyle"
        BasedOn="{StaticResource {x:Type TextBlock}}"
        TargetType="{x:Type TextBlock}">
        <Setter Property="FontSize" Value="28" />
        <Setter Property="LineHeight" Value="36" />
        <Setter Property="FontWeight" Value="SemiBold" />
    </Style>

    <Style
        x:Key="TitleLargeTextBlockStyle"
        BasedOn="{StaticResource {x:Type TextBlock}}"
        TargetType="{x:Type TextBlock}">
        <Setter Property="FontSize" Value="40" />
        <Setter Property="LineHeight" Value="52" />
        <Setter Property="FontWeight" Value="SemiBold" />
    </Style>

    <Style
        x:Key="DisplayTextBlockStyle"
        BasedOn="{StaticResource {x:Type TextBlock}}"
        TargetType="{x:Type TextBlock}">
        <Setter Property="FontSize" Value="68" />
        <Setter Property="LineHeight" Value="92" />
        <Setter Property="FontWeight" Value="SemiBold" />
    </Style>

</ResourceDictionary>
```
