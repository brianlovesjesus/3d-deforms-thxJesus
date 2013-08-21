/**
 * Metaball implementation v0.2.2 
 * Copyright 2007 by Brian R. Cowan http://www.briancowan.net/ 
 * Marching Cube tables tables by Paul Bourke at http://local.wasp.uwa.edu.au/~pbourke/geometry/polygonise/
 * Examples at http://www.briancowan.net/unity/fx
 *
 * Code provided as-is. You agree by using this code that I am not liable for any damage
 * it could possibly cause to you, your machine, or anything else. And the code is not meant
 * to be used for any medical uses or to run nuclear reactors or robots or such and so. 
 * 
 * Should be easily portable to any other language, all Unity Specific code is labeled so,
 * adapt it to any other environment. To use, attach the script to an empty object with a
 * Mesh Renderer and Mesh Filter. The created cubes will be one unit in size in XYZ.
 * Modify Update() and Start() to change the amount and movement and size of the blobs.
 *
 * Id love to see any project you use the code in.
 *
 * Mail any comments to: brian@briancowan.net (Vector on #IRC)
 *
 * Cheers & God bless
 */
 
/*Unity Specific*/
using UnityEngine;
using System.Collections;

public class MCBlob: MonoBehaviour {
	
	/*Amount of cubes in X/Y/Z directions, Dimension will always be from -.5f to .5f in XYZ
	  remember to call Regen() if changing!
    */
	int _dimX=30;
	int _dimY=30;
	int _dimZ=30;
	    		
	public int dimX {
		get {return _dimX; }
		set {_dimX=value; Regen(); }
	}
	public int dimY {
		get {return _dimY; }
		set {_dimY=value; Regen(); }
	}
	public int dimZ {
		get {return _dimZ; }
		set {_dimZ=value; Regen(); }
	}
	/*Blobs are a staggered array of floats, where first index is blob, and second is 0=x, 1=y 2=z 3=power
	  Multidim might be slightly faster, but staggered made the code a little cleaner IMO*/
	public float[][] blobs;
	
	/*Cutoff intensity, where the surface of mesh will be created*/
	public float isoLevel=.5f;	
	
	/*Scratch buffers for Vertices/Normals/Tris */
	private Vector3[] newVertex;
	private Vector3[] newNormal;
	private Vector2[] newUV;
	private int[] newTri;
	
	/*Pointer into scratch buffers for tris and vertices(also used for UVs and Normals)
	  at the end of a frame it will give the total amount of each*/
	private int triP=0;
	private int vertP=0;
	
	/*Generated at startup for dimX,dimY,dimZ, 
	  all the points, edges, cubes in the 3D lattice*/
	private mcPoint[] _points;
	private mcEdge[] _edges;
	private mcCube[] _cubes;


    /*Scratch buffers for use within functions, to eliminate the usage of new almost entirely each frame*/
	private Vector3[] tada;
	private Vector2[] tada2;
	private int tadac,tadac2;
	private int tadam=50000;
	
	/*Current frame counter*/
	private int pctr=0;
	
	/*Cube Class*/
	private class mcCube {	
		
		public mcCube()
		{
			cntr=0;
			edges=new mcEdge[12];
			for(int i=0;i<12;i++)
			{
				edges[i]=null;
			}
			points=new mcPoint[8];
		}
	
		
		/*12 Edges, see march() for their positioning*/
		public mcEdge[] edges;
		
		/*8 Points, see march() for their positioning*/ 
		public mcPoint[] points;
		
		/*last frame this cube was processed*/
		public int cntr;
		
		/*Pointers into the latice array*/
		public int px;
		public int py;
		public int pz;	
	}	
	
	/*Edge class*/
	private class mcEdge {
		
		/*the vector of the calculated point*/
		public Vector3 v3;
		
		/*index into newVertex/Normal/Uv of calculated point*/
		public int vi;
		
		/*Last frame this was calculated at*/
		public int cntr;
		
		/*axis of edge*/
		public int axisI;
		
		public mcEdge(int axisI)
		{
			this.cntr=0;
			this.axisI=axisI;
		}
		
	}
	
	/*Point (in lattice) class*/
	public class mcPoint {
		

		
		/*Calculated Intensity or Power of point*/
		public float _i;
		
		public int px,py,pz;
		
		private MCBlob mcblob;
		
		public int cntr;
	 
		/*Object Space position of point*/
	    public float[] index;
	    
		
		public mcPoint(float x,float y,float z,int px,int py,int pz,MCBlob thismcblob)
		{
			this.index=new float[3];
			index[0]=x;index[1]=y;index[2]=z;
			
			this.px=px; 
			this.py=py;
			this.pz=pz;
			this.cntr=0;
			this.mcblob=thismcblob;
		}
		
		/*Axis letter accessors*/
		public float x{
			get{ return index[0]; }
			set{ index[0]=value; }
		}
		public float y{
			get{ return index[1]; }
			set{ index[1]=value; }
		}
		public float z{
			get{ return index[2]; }
			set{ index[2]=value; }
		}
		
		
				
		/*Calculate the power of a point only if it hasn't been calculated already for this frame*/
		public float i()
		{
			float pwr;
			if(cntr<mcblob.pctr) {
				cntr=mcblob.pctr;
				pwr=0f;
				for(int jc=0;jc<this.mcblob.blobs.Length;jc++) {
					float[] pb=this.mcblob.blobs[jc];					
					pwr+=(1.0f/Mathf.Sqrt(((pb[0]-this.x)*(pb[0]-this.x))+((pb[1]-this.y)*(pb[1]-this.y))+((pb[2]-this.z)*(pb[2]-this.z))))*pb[3];
				}
				this._i=pwr;
			}
			return this._i;
		}
		
		public float this[int idx]
		{
			get{
				return index[idx];
			}
			set{
				index[idx]=value;
			}
		}
		
		
		
	}

	
	
	/* Normals are calculated by 'averaging' all the derivatives of the Blob power functions*/ 
	private Vector3 calcNormal(Vector3 pnt)
	{
		int jc;
		Vector3 result=tada[tadac++];
		result.x=0;result.y=0;result.z=0;
		for(jc=0;jc<blobs.Length;jc++)
		{
			float[] pb=blobs[jc];
			
			Vector3 current=tada[tadac++];
			current.x=pnt.x-pb[0];
			current.y=pnt.y-pb[1];
			current.z=pnt.z-pb[2];
			float mag=current.magnitude;
			float pwr=.5f*(1f/(mag*mag*mag))*pb[3];			
			result=result+(current*pwr);			
		}
		return result.normalized;
	}
	
	
	/*Given xyz indices into lattice, return referring cube */	
	private mcCube getCube(int x,int y,int z)
	{
		if(x<0 || y<0 || z < 0 || x>=dimX || y>=dimY || z>=dimZ) {return null;}
		return _cubes[z+(y*(dimZ))+(x*(dimZ)*(dimY))];
	}
	
	/*Given xyz indices into lattice, return referring vertex */
	private mcPoint getPoint(int x,int y,int z)
	{
		if(x<0 || y<0 || z < 0 || x>dimX || y>dimY || z>dimZ) {return null;}
		return _points[z+(y*(dimZ+1))+(x*(dimZ+1)*(dimY+1))];
	}
	
	/*Return the interpolated position of point on an Axis*/
	private Vector3 mPos(mcPoint a,mcPoint b,int axisI)
	{
		float mu = (isoLevel - a.i()) / (b.i() - a.i());
		Vector3 tmp=tada[tadac++];
		tmp[0]=a[0];tmp[1]=a[1];tmp[2]=a[2];
		tmp[axisI]=a[axisI]+(mu*(b[axisI]-a[axisI]));

	}
	
	/*If an edge of a cube has not been processed, find the interpolated point for 
	  that edge (assumes the boundary crosses the edge) and compute the normal
	  for that point, as well as assigning it an index into the vertex list*/   
	private void genEdge(mcCube cube,int edgei,int p1i,int p2i)
	{
		Vector3 v;
		mcEdge e=cube.edges[edgei];
		if(e.cntr<pctr) {
			
			v=mPos(cube.points[p1i],cube.points[p2i],e.axisI);
			e.v3=v;
			e.vi=vertP;
			newNormal[vertP]=calcNormal(v);
			newVertex[vertP++]=v;		
			e.cntr=pctr;		
			
		}  
		
	}
	
	/*Calculate a cube:
	  First set a boolean pointer made up of all the vertices within the cube
	  then (if not all in or out of the surface) go through all the edges that 
	  are crossed by the surface and make sure that a vertex&normal is assigned 
	  at the point of crossing. Then add all the triangles that cover the surface
	  within the cube.
	  Returns true if the surface crosses the cube, false otherwise.*/
	private bool doCube(mcCube cube)
	{		
		int edgec,vertc;
		edgec=0;vertc=0;
						
		int cubeIndex=0;
		
		if(cube.points[0].i()>isoLevel) {cubeIndex|=1;}						
		if(cube.points[1].i()>isoLevel) {cubeIndex|=2;}		
		if(cube.points[2].i()>isoLevel) {cubeIndex|=4;}
		if(cube.points[3].i()>isoLevel) {cubeIndex|=8;}
		if(cube.points[4].i()>isoLevel) {cubeIndex|=16;}												
		if(cube.points[5].i()>isoLevel) {cubeIndex|=32;}
		if(cube.points[6].i()>isoLevel) {cubeIndex|=64;}
		if(cube.points[7].i()>isoLevel) {cubeIndex|=128;}	
		
		int edgeIndex=edgeTable[cubeIndex];
		edgec+=edgeIndex;		
		if(edgeIndex!=0) {				
			if( (edgeIndex & 1) > 0) { genEdge(cube,0,0,1); }
			if( (edgeIndex & 2) > 0) { genEdge(cube,1,1,2);}
			if( (edgeIndex & 4) > 0) { genEdge(cube,2,2,3); }
			if( (edgeIndex & 0x8) > 0) { genEdge(cube,3,3,0); }
			if( (edgeIndex & 0x10) > 0) { genEdge(cube,4,4,5); }
			if( (edgeIndex & 0x20) > 0) { genEdge(cube,5,5,6); }
			if( (edgeIndex & 0x40) > 0) { genEdge(cube,6,6,7); }
			if( (edgeIndex & 0x80) > 0) { genEdge(cube,7,7,4); }										
			if( (edgeIndex & 0x100) > 0) { genEdge(cube,8,0,4); }
			if( (edgeIndex & 0x200) > 0) { genEdge(cube,9,1,5); }
			if( (edgeIndex & 0x400) > 0) { genEdge(cube,10,2,6); }
			if( (edgeIndex & 0x800) > 0) { genEdge(cube,11,3,7); }
			
			int tpi=0;
			int tmp;				
			while(triTable[cubeIndex,tpi]!=-1) {
				tmp=cube.edges[triTable[cubeIndex,tpi+2]].vi;
   				newTri[triP++]=tmp;vertc+=tmp;	
   				tmp=cube.edges[triTable[cubeIndex,tpi+1]].vi;
   				newTri[triP++]=tmp;vertc+=tmp;
   				tmp=cube.edges[triTable[cubeIndex,tpi]].vi;
   				newTri[triP++]=tmp;vertc+=tmp;
   				tpi+=3;   							  
			}
			
			return true;
		} else {
			return false;
		}					
	}
	
	/*Recurse all the neighboring cubes where thy contain part of the surface*/
	/*Counter to see how many cubes where processed*/
	int cubec;
	private void recurseCube(mcCube cube)
	{
		mcCube nCube;
		int jx,jy,jz;
		jx=cube.px; jy=cube.py; jz=cube.pz;
		cubec++;
		/* Test 6 axis cases. This seems to work well, no need to test all 26 cases */
		nCube=getCube(jx+1,jy,jz);
		if(nCube!=null && nCube.cntr<pctr) {nCube.cntr=pctr; if(doCube(nCube)) { recurseCube(nCube); }}
		nCube=getCube(jx-1,jy,jz);
		if(nCube!=null && nCube.cntr<pctr) {nCube.cntr=pctr; if(doCube(nCube)) { recurseCube(nCube); }}
		nCube=getCube(jx,jy+1,jz);
		if(nCube!=null && nCube.cntr<pctr) {nCube.cntr=pctr; if(doCube(nCube)) { recurseCube(nCube); }}
		nCube=getCube(jx,jy-1,jz);
		if(nCube!=null && nCube.cntr<pctr) {nCube.cntr=pctr; if(doCube(nCube)) { recurseCube(nCube); }}
		nCube=getCube(jx,jy,jz+1);
		if(nCube!=null && nCube.cntr<pctr) {nCube.cntr=pctr; if(doCube(nCube)) { recurseCube(nCube); }}
		nCube=getCube(jx,jy,jz-1);
		if(nCube!=null && nCube.cntr<pctr) {nCube.cntr=pctr; if(doCube(nCube)) { recurseCube(nCube); }}

		

		
	}
	
	/*Go through all the Blobs, and travel from the center outwards in a negative Z direction
	until we reach the surface, then begin to recurse around the surface. This isn't flawless
	if the blob isn't completely within the lattice boundaries in the minimal Z axis and no
	other blob that does check out is in contact with it. The blob will dissapear, but otherwise
	works well*/
	private void march()
	{
		int i,jx,jy,jz;
		for(i=0;i<blobs.Length;i++)
		{
			float[] pb=blobs[i];
			jx=(int)((pb[0]+.5f)*dimX);
			jy=(int)((pb[1]+.5f)*dimY);
			jz=(int)((pb[2]+.5f)*dimZ);
			
			
			while(jz>=0)
			{
				mcCube cube=getCube(jx,jy,jz);
				if(cube!=null && cube.cntr<pctr) {
					cube.cntr=pctr;
					if(doCube(cube)) {
						recurseCube(cube);						
						jz=-1;
					} 
				} else {
					jz=-1;
				}
				jz-=1;						
			}					 
		}
		
		
	}
	
 	
 	 	
 	
 	/*Unity and Sample Specific, scratch caches to not reallocate vertices/tris/etc...*/
 	Vector3[] fv,fn;
 	int[] ft;
 	Vector2[] fuv;
 
    //Last Status Post
 	private float lt=0f;
	
 	
 	/*Unity and Sample Specific*/
	private void renderMesh()
	{
		 int i;	
		
		transform.Rotate(Time.deltaTime*10f,0,Time.deltaTime*.6f);
				
		if(lt+1<Time.time) {
			lt=Time.time;
			GUIText guit=(GUIText) GameObject.Find("guit").guiText;
			guit.text="T:"+triP+" V:"+vertP+" C:"+cubec+" FPS:"+(int)(1f/Time.deltaTime);
		}
	   
		
		
		/*Clear the Vertices that don't have any real information assigned to them */
		for(i=0;i<vertP;i++) {fv[i]=newVertex[i];fn[i]=newNormal[i];		                      
							  fuv[i]=tada2[tadac2++];
							  Vector3 fuvt=transform.TransformPoint(fn[i]).normalized;							  
							  fuv[i].x=(fuvt.x+1f)*.5f;fuv[i].y=(fuvt.y+1f)*.5f;}							  
//							  fuv[i].x=fn[i].x;fuv[i].y=fn[i].y;}
							  
		for(i=vertP;i<fv.Length;i++) {fv[i][0]=0;fn[i][0]=0;fuv[i][0]=0;
							  fv[i][1]=0;fn[i][1]=0;fuv[i][1]=0;
							  fv[i][2]=0;}
							  
							  
		for(i=0;i<triP;i++) {ft[i]=newTri[i];}
		for(i=triP;i<ft.Length;i++) {ft[i]=0;}
		
		Mesh mesh=((MeshFilter) GetComponent("MeshFilter")).mesh;
				
		
	    mesh.vertices = fv ;
	    mesh.uv = fuv;
	    	    mesh.triangles = ft;	
	    mesh.normals = fn;
	    
	    /*For Disco Ball Effect*/
	    //mesh.RecalculateNormals();	
	
	
	}

    /*What is needed to do every frame for the calculation and rendering of the Metaballs*/
	void doFrame()
	{
		tadac=0;
		tadac2=0;
		cubec=0;
		pctr++;
		triP=0;
		vertP=0;		
		march();		
		renderMesh();		
	}
	
	/*Regenerate Lattice and Connections, when changing Dimensions of Lattice*/
	void Regen() {
		startObjs();	
		startEngine();	
	}
	
	
	
	//Unity and Sample specific
	void Update () {
		
		blobs[0][0]=.12f+.12f*(float)Mathf.Sin((float)Time.time*.50f);
		blobs[0][2]=.06f+.23f*(float)Mathf.Cos((float)Time.time*.2f);
		blobs[1][0]=.12f+.12f*(float)Mathf.Sin((float)Time.time*.2f);
		blobs[1][2]=-.23f+.10f*(float)Mathf.Cos((float)Time.time*1f);
		blobs[2][1]=-.03f+.24f*(float)Mathf.Sin((float)Time.time*.35f);
		blobs[3][1]=.126f+.10f*(float)Mathf.Cos((float)Time.time*.1f);
		blobs[4][0]=.206f+.1f*(float)Mathf.Cos((float)Time.time*.5f);
		blobs[4][1]=.056f+.2f*(float)Mathf.Sin((float)Time.time*.3f);
		blobs[4][2]=.25f+.08f*(float)Mathf.Cos((float)Time.time*.2f);
		
		doFrame();	
		
		
	}

	//Unity and Sample Specific
	void Start () {
		lt=0f;
		blobs=new float[5][];
		blobs[0]=new float[]{.16f,.26f,.16f,.13f};
		blobs[1]=new float[]{.13f,-.134f,.35f,.12f};
		blobs[2]=new float[]{-.18f,.125f,-.25f,.16f};
		blobs[3]=new float[]{-.13f,.23f,.255f,.13f};		
		blobs[4]=new float[]{-.18f,.125f,.35f,.12f};
	    
	    isoLevel=1.95f;
	    
	    Regen();
		
		
	    
	}
	
	
	
	/*Unity Specific starting of engine*/
	void startEngine()
	{
		((MeshFilter) GetComponent("MeshFilter")).mesh=new Mesh();
	}
	
	
	/*Generate the Cube Lattice
	  All shared vertices and edges are connected across cubes,
	  it's not perfect in that the edges along the lower index borders
	  are not connected, but all the rest are, this shouldn't make any
	  noticeable visual impact, and have no performance impact unless
	  a blob lies along those borders*/	
	private void startObjs()
	{
		int i; 
		float  jx,jy,jz;
		int ijx,ijy,ijz;
		int pointCount=((dimX+1)*(dimY+1)*(dimZ+1));
		int cubeCount=(dimX*dimY*dimZ);
		int edgeCount=(cubeCount*3)+((2*dimX*dimY)+(2*dimX*dimZ)+(2*dimY*dimZ))+dimX+dimY+dimZ; //Ideal Edge Count
		int edgeNow=edgeCount+((dimX*dimY)+(dimY*dimZ)+(dimZ*dimX))*2; //Haven't combined the edges of the 0 index borders
		
		//Should be a pretty safe amount
		int tmpv=(int)(dimX*dimY*dimZ/7);
		tadam=tmpv*4;
		fv=new Vector3[tmpv];
		fn=new Vector3[tmpv];
		fuv=new Vector2[tmpv];
		
		
		//Pretty save amount of Tris as well
		ft=new int[(int)(cubeCount*.75)];

		newVertex=new Vector3[300000];
		newTri=new int[300000];
		newNormal=new Vector3[300000];
		tada=new Vector3[tadam*2];
		tada2=new Vector2[tadam*2];
		
		//newUV=new Vector2[300000];
		
		_cubes=new mcCube[cubeCount];
		_points=new mcPoint[pointCount];
		_edges=new mcEdge[edgeNow];
		
		for(i=0;i<tadam*2;i++)
		{
			tada[i]=new Vector3(0,0,0);
			tada2[i]=new Vector2(0,0);
		}
		
		for(i=0;i<edgeNow;i++){
			_edges[i]=new mcEdge(-1);		
		}
		
		
		i=0;
		for(jx=0.0f;jx<=dimX;jx++){
			for(jy=0.0f;jy<=dimY;jy++) {
				for(jz=0.0f;jz<=dimZ;jz++){
					_points[i]=new mcPoint((jx/dimX)-.5f,(jy/dimY)-.5f,(jz/dimZ)-.5f,(int)jx,(int)jy,(int)jz,this);
					
					i++;
				}
			}
		}
		
		for(i=0;i<cubeCount;i++)
		{
			_cubes[i]=new mcCube();
		}
		int ep=0;
		
		mcCube c;		
		mcCube tc;
		
		i=0;
		
		
		int topo=0;
		for(ijx=0;ijx<dimX;ijx++){
			for(ijy=0;ijy<dimY;ijy++) {
				for(ijz=0;ijz<dimZ;ijz++) {
					
									
					c=_cubes[i];
					i++;
					c.px=ijx; c.py=ijy; c.pz=ijz;
					
					
					
					mcPoint[] cpt=c.points;
					cpt[0]=getPoint(ijx,ijy,ijz);
					cpt[1]=getPoint(ijx+1,ijy,ijz);
					cpt[2]=getPoint(ijx+1,ijy+1,ijz);
					cpt[3]=getPoint(ijx,ijy+1,ijz);
					cpt[4]=getPoint(ijx,ijy,ijz+1);
					cpt[5]=getPoint(ijx+1,ijy,ijz+1);
					cpt[6]=getPoint(ijx+1,ijy+1,ijz+1);
					cpt[7]=getPoint(ijx,ijy+1,ijz+1);
										
					
					mcEdge[] e=c.edges;
	
								
					e[5]=_edges[ep++];e[5].axisI=1;
					e[6]=_edges[ep++];e[6].axisI=0;
					e[10]=_edges[ep++];e[10].axisI=2;
					
				    tc=getCube(ijx+1,ijy,ijz);			   
				    if(tc!=null) {tc.edges[11]=e[10];tc.edges[7]=e[5];}
				    
				    tc=getCube(ijx,ijy+1,ijz);
				    if(tc!=null) {tc.edges[4]=c.edges[6];tc.edges[9]=c.edges[10];}
				    
				    tc=getCube(ijx,ijy+1,ijz+1);
				    if(tc!=null) {tc.edges[0]=c.edges[6];}
				    
				    tc=getCube(ijx+1,ijy,ijz+1);
				    if(tc!=null) {tc.edges[3]=c.edges[5];}
				    
				    tc=getCube(ijx+1,ijy+1,ijz);
				    if(tc!=null) {tc.edges[8]=c.edges[10];}
				    
				    tc=getCube(ijx,ijy,ijz+1);
				    if(tc!=null) {tc.edges[1]=c.edges[5];tc.edges[2]=c.edges[6];}
				    
				    if(e[0]==null) {
				    	e[0]=_edges[ep++];e[0].axisI=0;
				    }			    
				    if(e[1]==null) {
				    	e[1]=_edges[ep++];e[1].axisI=1;
				    }			    
				    if(e[2]==null) {
				    	e[2]=_edges[ep++];e[2].axisI=0;
				    } else { topo++; }
				    if(e[3]==null) {
				    	e[3]=_edges[ep++];e[3].axisI=1;
				    }
				    if(e[4]==null) {
				    	e[4]=_edges[ep++];e[4].axisI=0;
				    }
				    if(e[7]==null) {
				    	e[7]=_edges[ep++];e[7].axisI=1;
				    }
				    if(e[8]==null) {
				    	e[8]=_edges[ep++];e[8].axisI=2;
				    }
				    if(e[9]==null) {
				    	e[9]=_edges[ep++];e[9].axisI=2;
				    }
				    if(e[11]==null) {
				    	e[11]=_edges[ep++];e[11].axisI=2;
				    }
				    
				    
				    
				}
			}
		}
				
	}

	
	
	/*Courtesy of http://local.wasp.uwa.edu.au/~pbourke/geometry/polygonise/*/
	private int[,]	triTable = new int[,]
	
	private int[] edgeTable=new int[] {
	
		
}