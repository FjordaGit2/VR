# Lecturer scripts for ElevenLabs (Tasks 2 & 3)

**Total target duration: 16:00 (960 s)**  
**Speaking rate:** about 130–140 words per minute (formal academic delivery)  
**Files:** name clips `01` … `14` to match images `1.png` … `14.png` in `Assets/Sc2LectureHall/Images`

| Clip | Image | Audio | Duration |
|------|-------|-------|----------|
| 01 | 1.png | 1.mp3 | 1:05 (65 s) |
| 02 | 2.png | 2.mp3 | 1:06 (66 s) |
| 03 | 3.png | 3.mp3 | 0:40 (40 s) |
| 04 | 4.png | 4.mp3 | 1:03 (63 s) |
| 05 | 5.png | 5.mp3 | 0:43 (43 s) |
| 06 | 6.png | 6.mp3 | 0:37 (37 s) |
| 07 | 7.png | 7.mp3 | 0:36 (36 s) |
| 08 | 8.png | 8.mp3 | 0:32 (32 s) |
| 09 | 9.png | 9.mp3 | 0:39 (39 s) |
| 10 | 10.png | 10.mp3 | 0:36 (36 s) |
| 11 | 11.png | 11.mp3 | 0:31 (31 s) |
| 12 | 12.png | 12.mp3 | 0:49 (49 s) |
| 13 | 13.png | 13.mp3 | 0:39 (39 s) |
| 14 | 14.png | 14.mp3 | 6:28 (388 s) |
| **Sum** | | | **16:04 (964 s)** |

In ElevenLabs, use a clear academic voice and steady pace. Clip 14 is intentionally long so the full lecture matches the task block. If a take runs short, leave silence at the end of that clip so Unity keeps the slide visible until the section time ends.

---

## Clip 01 — Image 1.png (1:15)

Welcome to this laboratory session. The session introduces Unity as a development environment and proceeds through installation, project creation, and the construction of a simple three-dimensional scene.

Unity is a real-time development platform used for interactive applications, including games, simulations, and virtual reality systems. Scenes are composed from objects. Behaviours are attached through components and scripts. Results are previewed in Play Mode.

Official information, documentation, and download access are available from the Unity website. The website is the primary entry point for obtaining Unity Hub and the editor. The laboratory procedure that follows covers downloading Unity, examining the editor layout, and assembling a minimal project. The first topic is the Unity landing page and related online resources.

---

## Clip 02 — Image 2.png (1:15)

Installation should use a Long-Term Support release, abbreviated LTS. An LTS build is maintained for an extended period and is therefore preferred for laboratory work, teaching, and research relative to short-cycle technical streams that change more frequently.

The editor may be installed on Windows or on macOS. Unity Hub should be installed in parallel. Hub is the application that manages editor versions, creates projects, and opens existing projects with a compatible editor. Using Hub reduces version mismatch between projects and installed editors.

The recommended installation sequence is as follows. Download Unity Hub from the Unity website. Run the installer and complete any required account sign-in. Within Hub, add an LTS editor module for the required platform. When installation is complete, proceed to project creation.

---

## Clip 03 — Image 3.png (1:00)

Open Unity Hub and select Create New Project. Choose a three-dimensional template so that the initial scene includes a perspective camera and default lighting appropriate for spatial content. Assign a project name and a storage location with adequate disk capacity and a recoverable path.

Confirm the settings and select Create. Hub generates the project structure and launches the Unity Editor. The first open may require additional time while packages are imported. On completion, an empty scene is available for object placement.

---

## Clip 04 — Image 4.png (1:40)

The Unity Editor comprises several primary panels. The Hierarchy lists all GameObjects in the active scene. The Scene view provides the three-dimensional workspace for selection, transformation, and layout. The Game view displays the output of the main camera under Play Mode.

The Inspector presents properties of the selected object, including Transform values, attached components, and material references. The Project window lists Assets stored on disk, such as models, materials, scripts, and folders. The Console reports informational messages, warnings, and errors generated during editing and playback.

The toolbar includes Play, Pause, and Step. Play enters runtime. Pause suspends runtime. Step advances a single frame. These controls, together with the panels described above, constitute the standard workspace for the remaining laboratory steps.

---

## Clip 05 — Image 5.png (0:55)

Scene organisation begins with a parent object for environment content. In the Hierarchy, create an empty GameObject and rename it Environment. An empty GameObject contains a Transform and no mesh. It functions as a structural parent: child objects remain grouped, and the parent may later be moved or deactivated as a unit.

Creation is performed from the Hierarchy context menu or the GameObject menu by selecting Create Empty, followed by renaming. With Environment selected, geometry may be added as children of this object.

---

## Clip 06 — Image 6.png (0:55)

A ground surface is added next. Right-click the Environment object, select three-dimensional object, then Plane. A plane is a planar mesh suited to floors and ground surfaces because of its extent and simple topology.

Parenting the plane under Environment preserves Hierarchy clarity. The plane may still be selected independently for translation, rotation, or scale. The plane will serve as the surface to which materials and textures are applied in subsequent steps.

---

## Clip 07 — Image 7.png (0:55)

Material assets should be stored in a dedicated folder. Under Assets in the Project window, create a folder named Materials. Folder structure supports project maintainability as asset counts increase; textures, materials, and models become difficult to locate when left unstructured in the Assets root.

The Materials folder will hold the ground material created in the following steps. Establishing the folder before material creation is standard laboratory practice for asset organisation.

---

## Clip 08 — Image 8.png (0:55)

Within the Materials folder, create a new material and name it Green. A material defines surface appearance under the active shader, including base colour, smoothness, and related rendering parameters.

Create the asset by right-clicking in the Materials folder, selecting Create, then Material, and entering the name. Selection of the material populates the Inspector with shader properties for subsequent adjustment.

---

## Clip 09 — Image 9.png (1:00)

With the Green material selected, verify that the shader is configured for a standard opaque surface appropriate to solid geometry. Set the albedo colour to green. Albedo specifies the base colour of the surface prior to lighting evaluation.

Use the colour field in the Inspector to select and apply the green value. At this stage the plane does not yet display the colour in the Scene view, because the material has not been assigned to the mesh renderer. Assignment follows in the next step.

---

## Clip 10 — Image 10.png (1:00)

Assign the Green material to the plane by dragging the material from the Project window onto the plane in the Scene view or onto the plane entry in the Hierarchy. Alternatively, assign the material in the Mesh Renderer materials slot in the Inspector.

Following assignment, the plane appears green in the Scene view. This establishes the reference between the material asset and the rendered mesh: the renderer references the material, and the material parameters determine surface appearance.

---

## Clip 11 — Image 11.png (0:55)

Default material smoothness often produces a reflective finish. For a matte ground surface, select the Green material and set Smoothness to zero. Smoothness controls the sharpness of specular response.

A zero smoothness value removes high-gloss highlights and yields a diffuse appearance more consistent with a ground plane. Parameter adjustment of this type is a standard part of material preparation before texturing.

---

## Clip 12 — Image 12.png (1:10)

A grass image is now introduced as a texture. Import the grass texture into the Materials folder, or into Assets and relocate it beside the material. Select the texture asset and inspect its import settings.

On first import, Texture Type is commonly Default and Texture Shape is two-dimensional. These settings are appropriate for a colour map applied to three-dimensional geometry. Additional import parameters, including maximum size, filtering, and compression, affect quality and memory use. The following step alters Texture Type and therefore changes the asset classification within Unity.

---

## Clip 13 — Image 13.png (1:00)

Set the grass texture Texture Type to Sprite, two-dimensional and user interface, then apply the change. Sprite classification is used for interface imagery and two-dimensional sprites and regenerates the asset for contexts that require a sprite reference.

The change illustrates that import settings determine how an asset is interpreted by the editor and by runtime systems. For mesh texturing, Default may later be restored; the laboratory point is that import configuration is an explicit authoring step.

---

## Clip 14 — Image 14.png + extended conclusion (7:00)

Select the Green material and set the shader to Unlit Texture. An unlit shader displays texture colour without contribution from scene lights. Assign the grass texture to the base or main texture slot of the material. Following this assignment, the plane in the Scene view displays the grass image. The visible result is the end point of a complete authoring chain: an installed editor, a named project, an organised Hierarchy, a ground mesh, a material asset, texture import settings, and a shader pathway that samples that texture on the surface.

The present slide consolidates the laboratory objective. What appears to be a single textured plane is evidence that each preceding stage was completed in the correct order. If the material had not been created, the texture could not be referenced. If import settings had not been inspected, the asset type would remain ambiguous. If the plane had not been parented under Environment, the Hierarchy would already be less readable. Laboratory work in Unity rewards this sequential discipline. Omissions at early stages typically appear later as missing references, incorrect shading, or scenes that cannot be handed to another operator without explanation.

A structured recap follows. Unity was identified as a real-time development platform accessed through official online resources. Installation used a Long-Term Support editor and Unity Hub. A three-dimensional project was created with an explicit name and location. The editor layout was described: Hierarchy, Scene, Game, Inspector, Project, and Console, together with Play, Pause, and Step. An Environment parent was introduced. A plane was added as ground. A Materials folder was created. A material named Green was authored. Albedo colour was set and the material was assigned to the plane. Smoothness was reduced for a matte response. A grass texture was imported, classified, and finally applied through an Unlit Texture shader, yielding the result shown here.

Each stage corresponds to a transferable laboratory skill. Editor literacy precedes modelling and scripting. Hub literacy prevents version conflicts across machines and collaborators. Hierarchy literacy prevents unstructured scenes filled with default names. Material literacy separates geometry from appearance, which is required when one mesh must support multiple visual treatments. Texture literacy connects image assets on disk to surfaces in the scene. Shader literacy determines whether lighting is evaluated or bypassed. The unlit pathway prioritises clear verification of the texture itself before more complex lighting is introduced.

Assets must be distinguished from scene instances. The grass file and the Green material exist in the Project window as reusable assets. The plane exists in the Hierarchy as a scene object that references the material. Changing the material updates appearance wherever it is assigned. Duplicating the plane does not duplicate the material file; both instances can share one material reference. This separation supports scalable construction, consistent appearance across objects, and collaborative sharing of material libraries.

Project organisation is part of experimental method. Folders such as Materials, Textures, Models, and Scripts reduce search cost and duplication. The Materials folder created earlier is a minimal instance of that principle. Consistent naming, avoidance of spaces where pipelines are fragile, and clear folder boundaries reduce operator error during later sessions. The Environment parent illustrates the same principle in the scene graph: parent transforms propagate to children, and grouped content can be hidden, moved, or replaced as a unit. In virtual reality and simulation work, such structure also supports later separation of static scenery from dynamic actors and interactive objects.

Regarding materials and lighting: Standard opaque workflows respond to scene lights; Unlit Texture displays mapped colour directly. Smoothness and albedo remain central under lit shaders; under unlit shaders, the sampled texture dominates. Laboratory notes should record the shader used, because visual comparisons across conditions are otherwise invalid. Texture import settings likewise warrant documentation. Texture Type, shape, maximum size, filtering, and compression alter appearance and memory cost. The Sprite classification used earlier demonstrated that import settings are editable authoring decisions. For continued three-dimensional ground work, a Default colour map may later be restored once that principle is understood. Either choice is acceptable when it is intentional and recorded.

Quality control at the end of a Unity laboratory session should include a short checklist. Confirm that the correct scene is saved. Confirm that materials and textures remain referenced and are not pink or missing. Confirm that Hierarchy names remain meaningful. Confirm that Play Mode opens without Console errors related to missing assets. Confirm that the project path is backed up or version-controlled according to local policy. Confirm that the Unity version used to create the project is noted, so that Hub can reopen it with a compatible editor. These checks convert a demonstration into reproducible work.

Extension may add further geometry under Environment, additional materials for walls or props, scripts for interaction or logging, lighting for realism after unlit verification, audio sources, and multiple scenes in Build Settings for menus, practice blocks, and experimental tasks. Each extension still depends on Hub, editor panels, Hierarchy organisation, and the material–texture relationship illustrated on this slide. In research and teaching, the same methods scale from a textured ground plane to controlled environments used in experimental protocols, where visual content and timing must remain stable across participants.

Final summary. Unity Hub manages editors and projects. The Hierarchy, Scene, Game, Inspector, Project, and Console constitute the primary interface. Empty parents organise content. Planes provide ground geometry. Materials define surface parameters. Textures supply image data. Shaders determine display. The grass-covered plane shown here is the integrated result of those procedures.

This concludes the laboratory introduction to Unity.

---

## After recording

1. Export 14 clips named to match slides (`Lecturer_01` … `Lecturer_14` or similar).  
2. Drop them into `Assets/Additional Audios/` (or `Assets/Sc2LectureHall/Audios/`).  
3. On the `AnimAndImage` object in `Sc2LectureHall` and `Sc2BLectureHall`, assign:  
   - `lessonSprites` elements 0–13 → images 1–14  
   - `lessonVoiceClips` elements 0–13 → audio 01–14  
   - `imgLession` → the canvas Image  
   - `lecturerRoot` / `anim` → `LecturerAnim1`  
   - `lecturerVoice` → AudioSource on the lecturer (spatial blend as preferred)
