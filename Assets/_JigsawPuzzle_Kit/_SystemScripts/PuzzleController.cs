//----------------------------------------------------------------------------------------------------------------------------------------------------------
// Main script to prepare, control whole puzzle. Also processes decomposition and user input (like pieces movement)
//----------------------------------------------------------------------------------------------------------------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


// List of basic puzzle state to process
public enum PuzzleState {None, DragPiece, ReturnPiece, DropPiece, RotatePiece, PuzzleAssembled}


[AddComponentMenu("Scripts/Jigsaw Puzzle/Puzzle Controller")]
public class PuzzleController : MonoBehaviour 
{

	// Position/rotation of all pieces will be saved/restored 
	public bool enablePositionSaving = true;

	// Use pieces grouping (please don't use for imported and 3D puzzles)
	public bool enablePiecesGroups = true;

	// Should pieces be rotated during decomposition
	public bool randomizeRotation = false;

    // Pieces willn't be moved during decomposition (only rotated)
    public bool changeOnlyRotation = false;

    // Allow shifting(including at decomposition) pieces in all 3 dimensions, Require them to be strictly in 3D place for assembling
    public bool fullyIn3D = false;

    // Sides (around puzzle) where pieces should be moved during decomposition
    public bool decomposeToLeft = true;
	public bool decomposeToRight = true;
	public bool decomposeToTop;
	public bool decomposeToBottom;

	// Decomposition areas size and offset
	public Vector3 decomposeDistance  = new Vector3(5,5,5);
	public bool calculateDecomposeOffset;
	public Vector3 decomposeOffset = new Vector3(1,1,1);

	// Pieces movement/rotation properties
	public float movementTime = 0.1f;
	public float rotationSpeed = 100;
	public float dragOffsetZ = 2;
	public float dragTiltSpeed = 2;
	public float mobileDragOffsetY = 0.5f;

	// Allowed position/rotation offset to consider piece placed to it origin
	public float allowedDistance = 0.5f;
	public float allowedRotation = 10;

	// Change transparency for assembled pieces
	public float finalTransparency = 0.7f;



	// Important internal variables - please don't change them blindly    //[HideInInspector] 
	public bool invertedRules;
	public PuzzleState state;

	public Bounds puzzleBounds;
	public Bounds puzzleDefaultBounds;

	public Vector2 maxPieceSize; 
	public PuzzlePiece[] pieces;
	public List<int> movedPieces;

	int currentPiece = -1; 
	Vector3 pieceCenterOffset;
	Vector2 oldPointerPosition;
	 
	Material pieceMaterial_assembled; 

	List <PuzzlePiece> overlappedPieces;
	List<int> ungroupedPieces;

	PuzzlePieceGroup currentGroup = null;
	GameObject currentObject;
	Transform thisTransform;
	Transform currentObjectTransform;
    Quaternion puzzleInitialRotation;


    //===================================================================================================== 
    // Prepare puzzle for game
    public void Prepare ()
	{
		if (invertedRules)
			enablePiecesGroups = false;
				
		thisTransform = transform;

        puzzleInitialRotation = thisTransform.rotation;
        thisTransform.rotation = Quaternion.identity;


        pieces = new PuzzlePiece[thisTransform.childCount];

        // Material when piece assembled in puzzle
        if (pieces.Length > 0)
        {
            pieceMaterial_assembled = new Material(thisTransform.GetChild(0).GetComponent<Renderer>().sharedMaterial);
            pieceMaterial_assembled.color = new Color(pieceMaterial_assembled.color.r, pieceMaterial_assembled.color.g, pieceMaterial_assembled.color.b, finalTransparency);
        }
        else
            Debug.LogWarning("In some reason the amount of pieces is 0!");

		for (int i = 0; i < pieces.Length; i++) 
		{
			pieces[i] = new PuzzlePiece (thisTransform.GetChild(i), pieceMaterial_assembled);

			// Calculate maximal piece-size
			if (pieces[i].renderer.bounds.size.x > maxPieceSize.x)
				maxPieceSize.x = pieces[i].renderer.bounds.size.x;	
            
			if (pieces[i].renderer.bounds.size.y > maxPieceSize.y) 
				maxPieceSize.y = pieces[i].renderer.bounds.size.y;   
		} 

        thisTransform.rotation = puzzleInitialRotation;

        // Claculate bounds
        CalculateBounds();

        // Calculate decomposeOffset as half of maxPieceSize
        if (calculateDecomposeOffset) 
			decomposeOffset = maxPieceSize/2;


		// Prepare internal arrays
		movedPieces = new List<int>();
		ungroupedPieces = new List<int>();
		overlappedPieces = new List <PuzzlePiece>();
		PlayerPrefs.DeleteAll();

	}

	//-----------------------------------------------------------------------------------------------------	
	// Process puzzle during gameplay (including user input)
	public PuzzleState ProcessPuzzle (Vector3 _pointerPosition, bool _dragInput, float _rotationDirection)
	{
		if (IsAssembled()) 
			return PuzzleState.PuzzleAssembled;
		else 
			state = PuzzleState.None;


        // Check is any piece clicked and get it Id 
        if (_dragInput  &&  currentPiece < 0) 
		{
			_pointerPosition.z = 0;
			currentPiece = GetPointedPieceId(_pointerPosition, true);

			if (currentPiece >= 0) 
			{
				pieceCenterOffset = pieces [currentPiece].GetPieceCenterOffset();
									
				if (enablePiecesGroups) 
				{
					currentObject = PuzzlePieceGroup.GetGroupObjectIfPossible (pieces [currentPiece].transform.gameObject);
					currentObjectTransform = currentObject.transform;
					currentGroup = currentObject.GetComponent<PuzzlePieceGroup> ();
				} 
				else 
					{
						currentObject = pieces [currentPiece].transform.gameObject;
						currentObjectTransform = pieces [currentPiece].transform;
					}


                if (!changeOnlyRotation)
                    if (fullyIn3D)
                        currentObjectTransform.position = new Vector3(_pointerPosition.x - pieceCenterOffset.x, _pointerPosition.y - pieceCenterOffset.y, currentObjectTransform.position.z - dragOffsetZ);
                    else
                        currentObjectTransform.position = new Vector3(_pointerPosition.x - pieceCenterOffset.x, _pointerPosition.y - pieceCenterOffset.y, -dragOffsetZ);

                state = PuzzleState.DragPiece;
			} 

		}


		// If no piece is grabbed - RETURN
		if (currentObject == null) 
		{
			return PuzzleState.None;
		}
      
   
		// Pointer position offset for mobile
		#if !UNITY_EDITOR && !UNITY_STANDALONE && !UNITY_WEBPLAYER  && !UNITY_WEBGL
		    _pointerPosition.y += maxPieceSize.y * mobileDragOffsetY; 
        #else // for desktop
             if (changeOnlyRotation)
                    _rotationDirection = 1;
		#endif

				
			// Set currentObject center position  to pointerPosition
            if (!changeOnlyRotation)
			    currentObjectTransform.position = new Vector3 (_pointerPosition.x - pieceCenterOffset.x,  _pointerPosition.y - pieceCenterOffset.y, currentObjectTransform.position.z);
	

			// If piece rotation requested(by RMB or 2 touched) and possible - rotate piece around Z-axis
			if (_rotationDirection != 0  &&  randomizeRotation)
			{			
                pieceCenterOffset = pieces [currentPiece].GetPieceCenterOffset();
		        currentObjectTransform.RotateAround(pieces[currentPiece].renderer.bounds.center, Vector3.forward, _rotationDirection * rotationSpeed * Time.deltaTime);         
   
                state = PuzzleState.RotatePiece;
			}


			// Tilt piece according to movement direction if needed
			if (dragTiltSpeed > 0)
			{
				currentObjectTransform.localRotation = new Quaternion (
																		Mathf.Lerp (currentObjectTransform.localRotation.x, (oldPointerPosition.y - _pointerPosition.y) * dragTiltSpeed, Time.deltaTime),
																		Mathf.Lerp (currentObjectTransform.localRotation.y, (oldPointerPosition.x - _pointerPosition.x) * dragTiltSpeed, Time.deltaTime),
																		currentObjectTransform.localRotation.z,
																		currentObjectTransform.localRotation.w
																  	);
				oldPointerPosition = _pointerPosition;
			}



			// Drop piece and assemble it to puzzle (if it close enough to it initial position/rotation)    
			if (!_dragInput) 
			{   
				currentObjectTransform.localRotation = new Quaternion(0, 0, currentObjectTransform.localRotation.z, currentObjectTransform.localRotation.w); //Removes the tilt effect
                if (!changeOnlyRotation)
                    currentObjectTransform.position = new Vector3 (currentObjectTransform.position.x, currentObjectTransform.position.y, currentObjectTransform.position.z + dragOffsetZ);
				
				// Process groups if needed
				if (enablePiecesGroups) 
				{
					bool grouping = false;

					// Get list of all pieces overlapped by currentPiece
					foreach (int movedPieceId in movedPieces)
						if (movedPieceId != currentPiece)
							if (currentGroup != null) // if dropped a group
							{
								if (pieces [movedPieceId].transform.parent != currentGroup.transform)
									foreach (PuzzlePiece groupPiece in currentGroup.puzzlePieces)
										if (groupPiece.renderer.bounds.Intersects (pieces [movedPieceId].renderer.bounds))
											overlappedPieces.Add (pieces [movedPieceId]);
							} 
							else  // if dropped a piece
								if (pieces [currentPiece].renderer.bounds.Intersects (pieces [movedPieceId].renderer.bounds))
									overlappedPieces.Add (pieces [movedPieceId]);

					// Try to merge overlapped pieces to groups
					for (int i = 0; i < overlappedPieces.Count; i++) 
						grouping |= PuzzlePieceGroup.MergeGroupsOrPieces (pieces [currentPiece], overlappedPieces [i], this);
					overlappedPieces.Clear ();

					if (grouping) 
					{
						UpdateUngroupedPiecesList ();
						state = PuzzleState.DropPiece;
					}


					// Assemble grouped pieces to puzzle
					if (currentGroup != null)
					{
						if (IsPieceInPlace (currentGroup.puzzlePieces [0], allowedDistance, allowedRotation) || (invertedRules && !IsPieceInPlace (currentGroup.puzzlePieces [0], allowedDistance, allowedRotation))) 
						{
							int groupPieceId; 

							foreach (PuzzlePiece groupPiece in currentGroup.puzzlePieces) 
							{
								groupPiece.transform.parent = thisTransform;
								groupPieceId = GetPieceId (groupPiece);
					
								if (invertedRules)
									movedPieces.Remove (groupPieceId);
								else
									ReturnPiece (groupPieceId, movementTime);
							
							}
							Destroy (currentGroup.gameObject);

							state = PuzzleState.ReturnPiece;
						} 
						else // Just drop it
							state = PuzzleState.DropPiece;
					}

				}

				 //Assemble ungrouped piece to puzzle
				if (currentGroup == null)
					if (IsPieceInPlace(currentPiece, allowedDistance, allowedRotation) ||  (invertedRules && !IsPieceInPlace(currentPiece, allowedDistance, allowedRotation)) )
					{
						if (invertedRules) 
							movedPieces.Remove(currentPiece); 
						else
							ReturnPiece(currentPiece, movementTime);
	
						state = PuzzleState.ReturnPiece;
					}
					else // Just drop it
						state = PuzzleState.DropPiece;
						


				currentObject = null;
				currentPiece = -1;

			}


		return state;             
	}

    //----------------------------------------------------------------------------------------------------
    // Decompose puzzle (randomly moves pieces to specified decompose-areas)
    // If _filterList!=null - will process only pieces from the list
    public void DecomposePuzzle (bool _allowForUnassembled = false, List<int> _filterList = null)
	{ 
		if (!IsAssembled()  &&  !_allowForUnassembled) 
			return;

		Random.InitState(System.DateTime.Now.Millisecond);

		// Set list of decompose sides
		List<int> decomposeSides = new List<int>();
		if (decomposeToLeft) 	decomposeSides.Add(0);
		if (decomposeToTop) 	decomposeSides.Add(1);
		if (decomposeToRight) 	decomposeSides.Add(2);
		if (decomposeToBottom) 	decomposeSides.Add(3);

		int randomDecomposeSide;
		Vector3 targetPosition = new Vector3();


        // Start decomposition cycle 
       if (changeOnlyRotation)
        {
            for (int i = 0; i < pieces.Length; i++)
                if (_filterList == null || (_filterList != null && _filterList.Contains(i)))
                {
                    movedPieces.Add(i);

                    // Set random Z-rotation (if needed)
                    if (randomizeRotation)
                        pieces[i].transform.RotateAround(pieces[i].renderer.bounds.center, Vector3.forward, rotationSpeed * Random.rotation.z);
                }

        }
        else
            if (decomposeSides.Count > 0)
                for (int i = 0; i < pieces.Length; i++)
                    if (_filterList == null || (_filterList != null && _filterList.Contains(i)))
                    {
                        randomDecomposeSide = Random.Range(0, decomposeSides.Count);

                        if (fullyIn3D)
                            targetPosition.z = Random.Range(puzzleBounds.center.z + decomposeDistance.z / 2 + decomposeOffset.z, puzzleBounds.center.z - decomposeDistance.z / 2 + decomposeOffset.z);
                        else
                            targetPosition.z = i * -0.001f - 0.001f; //Random.Range(puzzleBounds.center.z + 0.001f, puzzleBounds.center.z - 0.001f);


                        switch (decomposeSides[randomDecomposeSide])
                        {
                            case 0: // Left
                                targetPosition.x = Random.Range(puzzleBounds.min.x - decomposeOffset.x - pieces[i].size.x, puzzleBounds.min.x - decomposeOffset.x - decomposeDistance.x);
                                targetPosition.y = Random.Range(puzzleBounds.center.y + decomposeDistance.y / 2 + pieces[i].size.y / 2, puzzleBounds.center.y - decomposeDistance.y / 2 + pieces[i].size.y / 2);
                                break;

                            case 1: // Top
                                targetPosition.x = Random.Range(puzzleBounds.center.x - decomposeDistance.x / 2, puzzleBounds.center.x + decomposeDistance.x / 2 - pieces[i].size.x);
                                targetPosition.y = Random.Range(puzzleBounds.max.y + decomposeOffset.y + pieces[i].size.y, puzzleBounds.max.y + decomposeOffset.y + decomposeDistance.y);
                                break;

                            case 2: // Right
                                targetPosition.x = Random.Range(puzzleBounds.max.x + decomposeOffset.x, puzzleBounds.max.x + decomposeDistance.x + decomposeOffset.x - pieces[i].size.x / 2);
                                targetPosition.y = Random.Range(puzzleBounds.center.y + decomposeDistance.y / 2 + pieces[i].size.y / 2, puzzleBounds.center.y - decomposeDistance.y / 2 + pieces[i].size.y / 2);
                                break;

                            case 3: // Bottom
                                targetPosition.x = Random.Range(puzzleBounds.center.x - decomposeDistance.x / 2, puzzleBounds.center.x + decomposeDistance.x / 2 - pieces[i].size.x);
                                targetPosition.y = Random.Range(puzzleBounds.min.y - decomposeOffset.y, puzzleBounds.min.y - decomposeOffset.y - decomposeDistance.y + pieces[i].size.y);
                                break;
                        }

                        // Initiate piece movement to it decomposed position                                                                                                         
                        MovePiece(i, targetPosition, false, movementTime / 2);
                               

                        // Set random Z-rotation (if needed)
                        if (randomizeRotation)
                             pieces[i].transform.RotateAround(pieces[i].renderer.bounds.center, Vector3.forward, rotationSpeed * Random.rotation.z);
                        // pieces[i].transform.localRotation = new Quaternion(pieces[i].transform.localRotation.x, pieces[i].transform.localRotation.y, Random.rotation.z, pieces[i].transform.localRotation.w); 

                    }


        if (enablePiecesGroups) 
			CreateUngroupedPiecesList ();

	}

    //----------------------------------------------------------------------------------------------------
    // Remix available pieces (that haven't been placed, except groups) / returns them to the decomposition areas off of the board. 
    public void ShufflePuzzle()
    {
        DecomposePuzzle(true, enablePiecesGroups ? ungroupedPieces : movedPieces);
    }

    //----------------------------------------------------------------------------------------------------
    // Skip randomized puzzle decomposition and use current pieces positions
    public void NonrandomPuzzle ()
	{ 
		if (!IsAssembled()) 
			return;
		
		invertedRules = true;

		for (int i = 0; i < pieces.Length; i++) 
		{
			movedPieces.Add (i); 
			ungroupedPieces.Add (i);
		}

	}

	//----------------------------------------------------------------------------------------------------     
	// Claculate bounds	and bottom-right/top-left corners of the puzzle in World space	 	        		 
	public void CalculateBounds()
	{
        if (pieces != null)
		{
            // Set bounds
            puzzleBounds.max = new Vector3(pieces[pieces.Length - 1].renderer.bounds.max.x, pieces[0].renderer.bounds.max.y, pieces[pieces.Length - 1].renderer.bounds.max.z);
            puzzleBounds.min = new Vector3(pieces[0].renderer.bounds.min.x, pieces[pieces.Length - 1].renderer.bounds.min.y, pieces[0].renderer.bounds.max.z);


            // Temporarily reset Puzzle and pieces rotation
            puzzleInitialRotation = thisTransform.rotation;  
            thisTransform.rotation = Quaternion.identity;

              // Set default(untransformed) bounds
              puzzleDefaultBounds.max = new Vector3(pieces[pieces.Length - 1].renderer.bounds.max.x, pieces[0].renderer.bounds.max.y, pieces[pieces.Length - 1].renderer.bounds.max.z);
              puzzleDefaultBounds.min = new Vector3(pieces[0].renderer.bounds.min.x, pieces[pieces.Length - 1].renderer.bounds.min.y, pieces[0].renderer.bounds.max.z);

            // Return proprer puzzle rotation 
            thisTransform.rotation = puzzleInitialRotation;

        }

    }

	//----------------------------------------------------------------------------------------------------
	// Check is piece close enough(within allowed position/rotation offset) to it origin to consider it returned (assembled to puzzle)
	public bool IsPieceInPlace (int _id, float _allowedDistance, float _allowedRotation) 
	{
		if (Vector3.Distance(pieces[_id].transform.position, thisTransform.TransformPoint(pieces[_id].startPosition)) < _allowedDistance 
				&&  (randomizeRotation ? Mathf.Abs(pieces[_id].transform.localRotation.z - pieces[_id].startRotation.z) * Mathf.Rad2Deg < _allowedRotation : true) ) 
			return true;
		else
			return false;

	}


	public bool IsPieceInPlace (PuzzlePiece _piece, float _allowedDistance, float _allowedRotation) 
	{
		if (Vector3.Distance(_piece.transform.position, thisTransform.TransformPoint(_piece.startPosition)) < _allowedDistance 
				&&  (randomizeRotation ? Mathf.Abs(_piece.transform.localRotation.z - _piece.startRotation.z) * Mathf.Rad2Deg < _allowedRotation : true) ) 
			return true;
		else
			return false;
	}


	//----------------------------------------------------------------------------------------------------
	// Get Id for piece under the pointer (calculates for all piecess or only moved)
	public int GetPointedPieceId (Vector3 _pointerPosition, bool _onlyMoved)
	{ 
		if (_onlyMoved)
			//for (int i = 0; i < movedPieces.Count; i++) 
            for (int i = movedPieces.Count - 1; i >= 0; i--)
			{
                _pointerPosition.z = pieces[movedPieces[i]].renderer.bounds.center.z;
                if (pieces[movedPieces[i]].renderer.bounds.Contains(_pointerPosition)) 
					return movedPieces[i];
			}
		else
            for (int i = pieces.Length - 1; i >= 0; i--)
			{
                _pointerPosition.z = pieces[movedPieces[i]].renderer.bounds.center.z;
                if (pieces[i].renderer.bounds.Contains(_pointerPosition))  
					return i;
			}


		return -1;  
	}

	//----------------------------------------------------------------------------------------------------
	// Get PuzzlePiece Id in pieces array
	public int GetPieceId (PuzzlePiece _piece) 
	{
		for (int i = 0; i < pieces.Length; i++) 
			if (_piece == pieces[i])  return i;

		return -1;  
	}

	//----------------------------------------------------------------------------------------------------
	// Return Id of currently interacted piece
	public PuzzlePiece GetCurrentPiece ()
	{
		if (currentPiece >= 0) return pieces[currentPiece];

		return null;     
	}

    //----------------------------------------------------------------------------------------------------
    // Initiate piece movement to new position (inLocal or World Space)
    public void MovePiece(int _id, Vector3 _targetPosition, bool _useLocalSpace, float _movementTime = -1)
    {
        if (_id < 0 && _id >= pieces.Length)
        { 
            Debug.Log("WARNING: No suitable piece for returning (all in place or grouped)");
            return;
        }

		
		if (_movementTime == 0) 
		{
			if (_useLocalSpace)
				pieces [_id].transform.localPosition = _targetPosition;
			else
				pieces [_id].transform.position = _targetPosition;
		}
		else 
			{
			    if (_movementTime < 0)
                    _movementTime = movementTime;	
                 
                StartCoroutine (pieces[_id].Move (_targetPosition, _useLocalSpace, _movementTime) );
            }


		if (_targetPosition == pieces [_id].startPosition) 
		{
			pieces [_id].Assemble ();
			movedPieces.Remove (_id);
			ungroupedPieces.Remove (_id);
		} 
		else 
			if (!movedPieces.Contains(_id))
                movedPieces.Add (_id);					
		
	}

	//----------------------------------------------------------------------------------------------------
	// Initiate piece (with custom Id) movement to it origin position
	public void ReturnPiece (int _id, float _movementTime = -1) 
	{	
		if (_id > pieces.Length)
			return;

		// If id < 0: try to get random piece
		if (_id < 0)
			if (enablePiecesGroups) 
			{
				if (ungroupedPieces.Count <= 0) return;
				_id = ungroupedPieces [Random.Range (0, ungroupedPieces.Count)]; 
			}
			else
				_id = movedPieces [Random.Range (0, movedPieces.Count)];
		
		if (randomizeRotation)
			pieces [_id].transform.localRotation = pieces [_id].startRotation;


		MovePiece (_id, pieces [_id].startPosition, true, _movementTime);
	}

	//----------------------------------------------------------------------------------------------------
	// Initiate currentPiece movement to it origin position
	public void ReturnCurrentPiece (float _movementTime = -1) 
	{ 
		MovePiece(currentPiece, pieces[currentPiece].startPosition, true, _movementTime);
		if (randomizeRotation)
			pieces[currentPiece].transform.localRotation = pieces[currentPiece].startRotation;

	}

	//----------------------------------------------------------------------------------------------------
	// Return all piecess to origins. Equal to full assembling of the puzzle
	public void ReturnAll (float _movementTime = -1) 
	{
		for (int i = 0; i < pieces.Length; i++)  
			MovePiece(i, pieces[i].startPosition, true, _movementTime);     
	}

	//----------------------------------------------------------------------------------------------------
	// Enable/Disable all pieces gameObjects
	public void SetPiecesActive (bool isActive) 
	{
		if (pieces != null  &&  pieces.Length > 0)
			if (pieces[0].transform.gameObject.activeSelf != isActive)
				for (int i = 0; i < pieces.Length; i++)  
				    pieces[i].transform.gameObject.SetActive(isActive);   
    }

	//----------------------------------------------------------------------------------------------------
	// Check is puzzle fully assembled or not
	public bool IsAssembled () 
	{
		if (movedPieces != null  &&  movedPieces.Count == 0)
			return true; 
		else 
			return false;
	}

	//-----------------------------------------------------------------------------------------------------	
	// Save puzzle progress (Assembled pieces only)
	public void SaveProgress (string _saveKey)
	{
		/*string saveString = "";
		for (int i = 0; i < pieces.Length; i++)
			if (!movedPieces.Contains(i)) saveString += (i.ToString()+"|");

		PlayerPrefs.SetString(_saveKey, saveString);
       
		// Save pieces positions/rotation if needed
		if (enablePositionSaving) 
		{
			saveString = "";
			Vector3 piecePosition; 
			for (int i = 0; i < movedPieces.Count; i++) 
			{
				piecePosition = pieces [movedPieces [i]].transform.position;
				saveString += (movedPieces [i].ToString () + "/" + piecePosition.x.ToString () + "/" + piecePosition.y.ToString () + "/" + piecePosition.z.ToString () + "/" + pieces [movedPieces [i]].transform.rotation.eulerAngles.z.ToString () + "|");
			}

            PlayerPrefs.SetString (_saveKey + "_Positions", saveString);	
		}*/

	}

	//-----------------------------------------------------------------------------------------------------	
	// Load puzzle progress (Assembled pieces only)
	public void LoadProgress (string _saveKey)
	{
		/*if (PlayerPrefs.HasKey(_saveKey)) 
		{
            // Load and place assembled pieces            
            string[] strings = PlayerPrefs.GetString(_saveKey).Split("|"[0]);
			for (int i = 0; i < strings.Length-1; i++)  
				ReturnPiece (int.Parse(strings[i]), 0); 

			// If needed - Load pieces positions and clamp groups
			if (enablePositionSaving  &&  PlayerPrefs.HasKey (_saveKey + "_Positions")) 
			{
				strings = PlayerPrefs.GetString (_saveKey + "_Positions").Split ("|" [0]);
				string[] pieceData;
				Vector3 pieceRotation;

                for (int i = 0; i < strings.Length - 1; i++) 
				{
					pieceData = strings [i].Split ("/" [0]);
					currentPiece = int.Parse (pieceData [0]);
                    
					MovePiece (currentPiece, new Vector3 (float.Parse (pieceData [1]), float.Parse (pieceData [2]), float.Parse (pieceData [3])), false, 0); 

					if (randomizeRotation) 
					{
						pieceRotation = pieces [currentPiece].transform.rotation.eulerAngles;
						pieces [currentPiece].transform.Rotate (new Vector3 (pieceRotation.x, pieceRotation.y, float.Parse (pieceData [4])));
					}

				}
				currentPiece = -1;

				Invoke ("TryToClampAllGroups", 0.01f);
			}				
			
		}
		else
			Debug.Log("No saved data found for: " + _saveKey);
		*/
	}

    //----------------------------------------------------------------------------------------------------
    // Reset puzzle progress and saves
    public void ResetProgress(string _saveKey)
    {
        StopAllCoroutines();

        PlayerPrefs.SetString(_saveKey, "");
        PlayerPrefs.DeleteKey(_saveKey + "_Positions");

        state = PuzzleState.None;
        currentPiece = -1;
        ungroupedPieces.Clear();
        movedPieces.Clear();
        overlappedPieces.Clear();

        for (int i = 0; i < pieces.Length; i++)
        {
          pieces[i].transform.parent = transform;
          pieces[i].transform.position = pieces[i].startPosition;
          pieces[i].transform.rotation = pieces[i].startRotation;          
        }

    }

    //----------------------------------------------------------------------------------------------------
    // Updates list of moved and ungrouped pieces Id
    public void UpdateUngroupedPiecesList ()
	{
		for (int i = 0; i < ungroupedPieces.Count; i++) 
			if (pieces [ungroupedPieces [i]].transform.parent != thisTransform) 
			{
				ungroupedPieces.RemoveAt (i);
				i--;
			}
	}

	//----------------------------------------------------------------------------------------------------
	// Сreate list of moved and ungrouped pieces Id
	public void CreateUngroupedPiecesList()
	{
		ungroupedPieces.Clear ();
		foreach (int pieceId in movedPieces) 
			if (pieces[pieceId].transform.parent == thisTransform)
				ungroupedPieces.Add (pieceId);
	}

	//-----------------------------------------------------------------------------------------------------	
	// Try to find and clamp all pieces/groups
	public void TryToClampAllGroups ()
	{
		if (enablePiecesGroups) 
		{
			overlappedPieces.Clear ();
			for (int i = 0; i < movedPieces.Count; i++) 
			{	
				currentObjectTransform = pieces [movedPieces [i]].transform;
					
				foreach (int movedPieceId in movedPieces)
					if (movedPieces [i] != movedPieceId)
						if (currentObjectTransform.parent == thisTransform  ||  (currentObjectTransform.parent != thisTransform  &&  currentObjectTransform.parent != pieces [movedPieceId].transform.parent))
							if (pieces [movedPieces [i]].renderer.bounds.Intersects (pieces [movedPieceId].renderer.bounds))
								overlappedPieces.Add (pieces [movedPieceId]);
				

				for (int j = 0; j < overlappedPieces.Count; j++)
					PuzzlePieceGroup.MergeGroupsOrPieces (pieces [movedPieces [i]], overlappedPieces [j], this);
				overlappedPieces.Clear ();
			}				
				

			CreateUngroupedPiecesList ();
		}

	}

    //===================================================================================================== 
    // Automatically generate puzzle background from the source image
    public SpriteRenderer GenerateBackground(Texture2D _image = null)
    {
        if (_image == null)
            {
                Debug.Log("There's no source image passed (or puzzle generated)");
                return null;
            }

        GameObject background = new GameObject();
        background.name = _image.name + "_background";

        SpriteRenderer spriteRenderer = background.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = Sprite.Create(_image, new Rect(0.0f, 0.0f, _image.width, _image.height), new Vector2(0, 1));

        return spriteRenderer;
    }

    //-----------------------------------------------------------------------------------------------------	
    // Automatically align puzzle/background with camera center
    public void AlignWithCameraCenter(Camera _camera, bool changeCameraSize = true, float _cameraSizeTuner = 0)
    {
        if (_camera == null)
            _camera = Camera.main;

        if (_camera)
        {
            Vector2 screenviewCenter = _camera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, _camera.nearClipPlane));
            transform.position = new Vector3(screenviewCenter.x - puzzleBounds.size.x / 2, screenviewCenter.y + puzzleBounds.size.y / 2, transform.position.z);
            Prepare();

            // Change camera view size according to size of (puzzle + decompose areas)
            if (_camera.orthographic  &&  changeCameraSize)
                _camera.orthographicSize = Mathf.Ceil((puzzleBounds.size.x + (decomposeDistance.x + decomposeOffset.x)*2)/4) + _cameraSizeTuner;

        }
        else
            Debug.Log("Please ensure that _camera exists");
    }

    //===================================================================================================== 
    // Utility visualization function
    public void OnDrawGizmos () 
	{
		
        if (movedPieces != null)
            for (int i = 0; i < movedPieces.Count; i++)
            {
                if (currentPiece == i)
                    Gizmos.color = Color.green;
                else
                    Gizmos.color = Color.white;

                Gizmos.DrawWireCube(pieces[movedPieces[i]].renderer.bounds.center, pieces[movedPieces[i]].renderer.bounds.size);
            }


		// Set color according to Application state and  re-calculate bounds(only in editor mode)
		if (!Application.isPlaying) 
			Gizmos.color = new Color ( 0.25f, 0.25f, 0.75f, 0.5f);
		else
			Gizmos.color = new Color ( 0.5f, 0.5f, 0.5f, 0.3f);


		// Draw a semitransparent cubes to visualize decomposition areas 
		Vector3 position;
		if (decomposeToLeft) 
		{
			position = new Vector3(puzzleBounds.min.x - decomposeOffset.x - decomposeDistance.x/2,  puzzleBounds.center.y, puzzleBounds.center.z + decomposeOffset.z);
			Gizmos.DrawCube (position, new Vector3 (decomposeDistance.x, decomposeDistance.y, decomposeDistance.z));
		}

        if (decomposeToTop)
		{
			position = new Vector3(puzzleBounds.center.x, puzzleBounds.max.y + decomposeDistance.y/2 + decomposeOffset.y, puzzleBounds.center.z + decomposeOffset.z);
            Gizmos.DrawCube(position, new Vector3(decomposeDistance.x, decomposeDistance.y, decomposeDistance.z));
        }

		if (decomposeToRight)
		{
			position = new Vector3(puzzleBounds.max.x + decomposeOffset.x + decomposeDistance.x/2, puzzleBounds.center.y, puzzleBounds.center.z + decomposeOffset.z);
            Gizmos.DrawCube(position, new Vector3(decomposeDistance.x, decomposeDistance.y, decomposeDistance.z));
        }

		if (decomposeToBottom)
		{
			position = new Vector3(puzzleBounds.center.x, puzzleBounds.min.y - decomposeDistance.y/2 - decomposeOffset.y, puzzleBounds.center.z + decomposeOffset.z);
            Gizmos.DrawCube(position, new Vector3(decomposeDistance.x, decomposeDistance.y, decomposeDistance.z));
        }


		// Draw line from top-left corner of puzzle to it  bottom-right corner
		Gizmos.DrawLine (new Vector3 (puzzleBounds.min.x, puzzleBounds.max.y, puzzleBounds.center.z), new Vector3 (puzzleBounds.max.x, puzzleBounds.min.y, puzzleBounds.center.z));   
	}

	//-----------------------------------------------------------------------------------------------------	
}