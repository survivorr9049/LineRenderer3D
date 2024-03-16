# LineRenderer3D
Adds functionality similiar to Unity's built-in LineRenderer, but with actual 3D geometry

<img src="line3d.png" width="600" >

## Performance 
This component was built with performance in mind, thanks to leveraging Job System and Burst compiler it is capable of generating huge amounts of geometry without causing a significant performance impact, generating a line with 2048 points at 16 resolution (65k triangles) takes roughly 1ms in Editor on my Ryzen 7 5700X
# Usage
-----
## Features
Similiar implementations often encounter issues with uneven thickness due to skewing and inconsistent alignment of vertices, this implementation makes sure that all generated meshes remain high quality and automatically fixes all of these issues
### Node twisting fix
![Twisting fix](twisting.gif)
### Consistent thickness fix
![Thickness fix](scaling.gif)
