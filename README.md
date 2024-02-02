# Unity Offline Depth Of Field

High Quality Offline Depth of Field.

This has none of the short comings of regular Depth of Field effects, and it even works with transparent materials/objects. It works by simply capturing the scene from many different angles in a circle, looking towards the focal point, and accumulating the captures into a render target which then gets saved to the disk.

![Render](GithubContent/Render.png)

## Comparisons

#### Offline DOF: Near Focus
![Render1a](GithubContent/Render1a.png)

#### Runtime DOF: Near Focus
![Render1b](GithubContent/Render1b.png)

#### Offline DOF: Far Focus
![Render2a](GithubContent/Render2a.png)

#### Runtime DOF: Far Focus
![Render2b](GithubContent/Render2b.png)

#### Offline DOF: Transparent Objects In Foreground
![RenderTransparency1a](GithubContent/RenderTransparency1a.png)

#### Runtime DOF: Transparent Objects In Foreground
![RenderTransparency1b](GithubContent/RenderTransparency1b.png)

#### Offline DOF: Focusing on Transparent Object
![RenderTransparency2a](GithubContent/RenderTransparency2a.png)

#### Runtime DOF: Focusing on Transparent Object
![RenderTransparency2b](GithubContent/RenderTransparency2b.png)