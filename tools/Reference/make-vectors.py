# The vectors the tests hold our generation against: what 3DSRNGTool's own code gives, written down once.
# Needs this folder's harness (ref7.csproj) built, with 3DSRNGTool cloned into ./3DSRNGTool; the tests themselves need neither.
#   python3 make-vectors.py ../../tests/AlolaDexMaker.Core.Tests/vectors
import subprocess, random, os, json, sys
HERE=os.path.dirname(os.path.abspath(__file__))
RES=HERE+'/3DSRNGTool/3DSRNGTool/Resources/bytes'
DOT=os.environ.get('DOTNET','dotnet')
OUT=sys.argv[1]
REF=[DOT,HERE+'/bin/Release/net10.0/ref7.dll']
rnd=random.Random(20260927)
def run(args):
    r=subprocess.run(REF+args,capture_output=True,text=True)
    assert r.returncode==0,(args,r.stderr[:300])
    return [json.loads(l) for l in r.stdout.strip().split('\n')]
def seed(): return '%08X'%rnd.getrandbits(32)

still=[]
for (sp,fo,ver) in [(144,0,'US'),(145,0,'US'),(384,0,'US'),(485,0,'US'),(380,0,'UM'),(641,0,'US'),(716,0,'US'),(793,0,'US'),(794,0,'US'),(795,0,'UM'),(796,0,'US'),(797,0,'UM'),(798,0,'US'),(799,0,'US'),(772,0,'US'),(789,0,'US'),(803,0,'US'),(718,1,'US'),(728,0,'US')]:
    for charm in ('1','0'):
        s=seed(); lo=478+rnd.randrange(0,200); hi=lo+120; tsv=rnd.randrange(4096); trv=rnd.randrange(16)
        rows=run(['sta',RES,str(sp),str(fo),ver,s,str(lo),str(hi),str(tsv),str(trv),charm])
        still.append({'species':sp,'form':fo,'version':ver,'seed':s,'first':lo,'last':hi,'tsv':tsv,'charm':charm=='1','rows':[r for r in rows if r['frame']<=hi]})
json.dump(still,open(OUT+'/still.json','w'),separators=(',',':'))

wild=[]
for (loc,idx,ver,night,ub) in [(8,1,'US','0',0),(8,1,'US','1',0),(6,0,'US','0',0),(136,1,'US','0',0),(136,1,'UM','1',0),(86,1,'US','0',0),(86,2,'UM','0',0),(164,0,'UM','0',805),(164,0,'US','1',806),(166,1,'US','0',0)]:
    for charm in ('1','0'):
        s=seed(); lo=478+rnd.randrange(0,200); hi=lo+120; tsv=rnd.randrange(4096); trv=rnd.randrange(16)
        rows=run(['wild',RES,str(loc),str(idx),ver,night,str(ub),s,str(lo),str(hi),str(tsv),str(trv),charm])
        for r in rows: r.pop('item',None)
        wild.append({'location':loc,'index':idx,'version':ver,'night':night=='1','beast':ub,'seed':s,'first':lo,'last':hi,'tsv':tsv,'charm':charm=='1','rows':[r for r in rows if r['frame']<=hi]})
json.dump(wild,open(OUT+'/wild.json','w'),separators=(',',':'))

# Draws once made for the sixth generation's hordes, no longer tested: kept so that the vectors after them come out as they were.
for sp in (590,198,707):
    for charm in ('1','0'):
        seed(); rnd.randrange(0,3000); rnd.randrange(4096); rnd.randrange(16)

eggs=[]
for k in range(24):
    st=','.join('%08X'%rnd.getrandbits(32) for _ in range(4))
    lo=rnd.randrange(0,100); hi=lo+60; tsv=rnd.randrange(4096); trv=rnd.randrange(16)
    charm=rnd.randrange(2); mm=rnd.randrange(2)
    ratio=[0x1F,0x3F,0x7F,0xBF,0xE1,0x00,0xFE,0xFF][k%8]; nido=1 if k%11==0 else 0
    mi=rnd.randrange(3); fi=rnd.randrange(3)
    miv=[rnd.randrange(32) for _ in range(6)]; fiv=[rnd.randrange(32) for _ in range(6)]
    ab=k%3; homo=rnd.randrange(2); fd=rnd.randrange(2)
    rows=run(['egg7',RES,st,str(lo),str(hi),str(tsv),str(trv),str(charm),str(mm),str(ratio),str(nido),str(mi),str(fi),'/'.join(map(str,miv)),'/'.join(map(str,fiv)),str(ab),str(homo),str(fd)])
    eggs.append({'state':st,'first':lo,'last':hi,'tsv':tsv,'charm':charm==1,'masuda':mm==1,'ratio':ratio,'eitherSex':nido==1,'maleHolds':mi,'femaleHolds':fi,'maleIvs':miv,'femaleIvs':fiv,'ability':ab,'sameSpecies':homo==1,'femaleIsDitto':fd==1,'rows':rows})
json.dump(eggs,open(OUT+'/egg7.json','w'),separators=(',',':'))
print({k:(len(v),sum(len(c['rows']) for c in v)) for k,v in [('still',still),('wild',wild),('egg7',eggs)]})
