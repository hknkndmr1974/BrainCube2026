TABLE OF CONTENTS
=================

1. OVERVIEW
2. INSTRUCTIONS
3. CREDITS

====================
===== OVERVIEW ===== 
====================

- Bold UI system:
  * Currencies & Energy
  * Inventory Items
  * Achievements
  * Player Interactions
  * Customization
  * Social Feature Visual Elements
  * Buttons
  * Ribbons
  * Fonts
  * Fontstyles
  * Popups
  * Wallets
  * Flat Icons
  * Screen Examples
  * VFX
  * Button behavior examples
  * Animations examples

- Optimized for all screen sizes
- Ready-to-use prefabs
- Example scene included

====================
=== INSTRUCTIONS ===
====================

1. Open the main ExampleScene.unity at:
   "Assets/BoldUISystem/Content/Examples/ExampleScene.unity"
   
   Specific example scenes:
   Built-In VFX: "Assets/BoldUISystem/Content/FX/Built-in/Examples/ExampleScene.unity"
   URP VFX: "Assets/BoldUISystem/Content/FX/URP/Examples/ExampleScene.unity"
   Icons: "Assets/BoldUISystem/Content/Icons/Examples/ExampleScene_AllIcons.unity"

2. Prefab System:
   - Content is organized as prefab variants inheriting from a base prefab.  
   - Modify the base prefab to apply changes globally.

3. For optimal build size:
   - Remove unused icon sets or disable "Include in Build" for any unused 'Sprite Atlases' to reduce build size.


   Note.

   VFX content was created using Unity's **URP** AND **Built-in Render Pipeline**. All included materials use default shaders compatible with both render pipelines.

If you are using a different Render Pipeline (such as **HDRP**), the effects may appear pink or broken. To make them work in your project:

* Go to:
Assets/BoldUISystem/Content/FX/URP/Materials

* Replace the two particle materials with equivalents that are compatible with your target Render Pipeline.
   - For HDRP, recreate the materials using HDRP Shader Graph.

Once the materials are replaced, the VFX effects should work correctly in your pipeline.


====================
==== CREDITS ======
====================

This asset pack was created by UIUXForgeDev.
Contact: uiuxforgedev@gmail.com



              uiuxforgedev@gmail.com
                       ....                       
               ..';:looddddoolc;'..               		
            .':okO00KKKKKKKKKKK0Okoc'.            
          .;dO0KK0kdoccloolccldk0KK0Od:.          
        .:x0KKOxc,..  'dOOd'  ..'cdOKK0x:.        
       'd0KKOdl;.     :OKKO:     .;cdOKK0d,       
      ,kKK0x;:k0kc.   :0KKO:   .ck0k:,d0KKk;.     
     ,xKK0o. .lOKKkc. ,x00x, .ckKKkc. .l0KKk,     
    .oKKKd.    'lkKO:. .''. .:O0kl.    .oKKKd.    
    ;OKKO;      ..;,.        .,;.       ,kKKO:    
   .c0KKOdooooolollllooollllloollolloloodOKKKl.   
   .ckOOOOOOOOOOO---UIUXFORGE---OOOOOOOOOOOOOc.   
    ;dxxd:'''''''',,'''''''''',,'''''''';oxxx:.   
    'oxxd;.     .;lc'        'cc;.      ;dxxo'    
    .;dxxo,   .:oxxo,..;::;..,oxxo:.   'oxxx:.    
     .cxxxd;.,oxxd:.  ;xkkd;  .:dxxo,.,oxkxc.     
      .:xkkxl:cc;.    ;xkkx;    .:lc:cxkkxc.      
       .,okkkxl;.     ;xkkx;     .,ldkkkd;.       
         .;okkkkdl:,..'cllc'..,:ldkkkkd:.         
           .,cdkkOOkxxddddddxxkkkOkdl,.           
              .':loxkkkkOOOkkkxol:,.              
                  ...',,,,,,'...         