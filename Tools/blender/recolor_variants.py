import numpy as np
from PIL import Image
import os, sys
"""Builds the colour-variant textures in Assets/Resources/Models/Characters/Variants (see ModelLibrary.ApplyVariant)
from the base-colour textures embedded in Operator.fbx, Infantry.fbx and Mercenary.fbx.

python3 recolor_variants.py <dir with Operator_atlas, Infantry_PackedMaterial1mat/2mat, Mercenary_atlas images> <Variants dir>
(the textures can be pulled out of the FBX files with Blender: image.save_render, or Unity's extracted .fbm folders)
Only the clothing hues are shifted; skin tones are left alone.
"""
SRC, DST = sys.argv[1], sys.argv[2]
def find(name):
    for ext in (".jpg", ".png", ".jpg.png"):
        if os.path.exists(os.path.join(SRC, name + ext)):
            return os.path.join(SRC, name + ext)
    raise FileNotFoundError(name)
def load(p):
    a=np.asarray(Image.open(p).convert('RGB')).astype(np.float32)/255
    return a
def hsv(a):
    r,g,b=a[...,0],a[...,1],a[...,2]
    mx=a.max(-1); mn=a.min(-1); d=mx-mn+1e-6
    h=np.where(mx==r,((g-b)/d)%6,np.where(mx==g,(b-r)/d+2,(r-g)/d+4))*60
    s=np.where(mx>0,(mx-mn)/(mx+1e-6),0); return h,s,mx
def hsv2rgb(h,s,v):
    h=(h%360)/60; i=np.floor(h).astype(int)%6; f=h-np.floor(h)
    p=v*(1-s); q=v*(1-s*f); t=v*(1-s*(1-f))
    out=np.zeros(h.shape+(3,),np.float32)
    for k,(R,G,B) in enumerate([(v,t,p),(q,v,p),(p,v,t),(p,q,v),(t,p,v),(v,p,q)]):
        m=i==k; out[m,0]=R[m]; out[m,1]=G[m]; out[m,2]=B[m]
    return out
def save(a,p):
    os.makedirs(os.path.dirname(p),exist_ok=True)
    Image.fromarray((np.clip(a,0,1)*255+0.5).astype(np.uint8)).save(p,quality=90)
def soft(x,lo,hi,w):  # 1 inside [lo,hi], fading over w
    return np.clip(np.minimum((x-lo+w)/w,(hi+w-x)/w),0,1)

# Operator (olive) -> desert / night
a=load(find('Operator_atlas')); h,s,v=hsv(a)
m=(soft(h,40,140,8)*soft(s,0.07,1,0.04))[...,None]
d=hsv2rgb(np.full_like(h,34.0),np.clip(s*1.15+0.08,0,0.55),np.clip(v*1.35+0.04,0,1))
save(a*(1-m)+d*m,os.path.join(DST,'OperatorDesert','Operator_atlas.jpg'))
n=hsv2rgb(np.full_like(h,215.0),np.clip(s*0.25,0,0.12),v*0.5)
save(a*(1-m)+n*m,os.path.join(DST,'OperatorNight','Operator_atlas.jpg'))

# Infantry (desert digital camo) -> woodland digital camo
pal=np.array([[0.05,0.05,0.04],[0.24,0.18,0.12],[0.26,0.31,0.17],[0.42,0.45,0.30],[0.55,0.52,0.40]],np.float32)
for mat in ['PackedMaterial1mat','PackedMaterial2mat']:
    a=load(find('Infantry_'+mat)); h,s,v=hsv(a)
    m=(soft(h,18,62,6)*soft(s,0.10,1,0.04)*soft(v,0.25,1,0.08))[...,None]
    L=np.clip((v-0.3)/0.55,0,1)*(len(pal)-1); i=np.clip(np.floor(L).astype(int),0,len(pal)-2); f=(L-i)[...,None]
    w=pal[i]*(1-f)+pal[i+1]*f
    save(a*(1-m)+w*m,os.path.join(DST,'InfantryWoodland','Infantry_'+mat+'.jpg'))

# Mercenary (grey-green) -> urban black
a=load(find('Mercenary_atlas')); h,s,v=hsv(a)
m=(soft(h,45,170,10)*soft(s,0.05,1,0.03))[...,None]
u=hsv2rgb(np.full_like(h,220.0),np.clip(s*0.3,0,0.15),v*0.45)
save(a*(1-m)+u*m,os.path.join(DST,'MercenaryUrban','Mercenary_atlas.jpg'))
print("ok")
