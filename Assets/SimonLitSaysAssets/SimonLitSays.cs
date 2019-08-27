using System;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;

using RNG = UnityEngine.Random;

public class SimonLitSays : MonoBehaviour
{
	// Standardized logging
	private static int globalLogID = 0;
	private int thisLogID;

	// Twitch Plays
	protected bool TwitchPlaysActive;

	public KMNeedyModule bombModule;
	public KMGameInfo bombInfo; // Needed to catch lights status
	public KMAudio bombAudio;

	public TextMesh mainScreen;
	public GameObject[] keypad;

	public Material LEDCorrect;
	public Material LEDWrong;
	public Material LEDOff;
	public Material[] kpMats;

	private TextMesh[] kpLabels;
	private KMSelectable[] kpSelects;
	private Renderer[] kpRends;
	private Renderer[] kpLEDs;
	private Animator[] kpAnims;

	private readonly string[] __colors = new string[] {
		"white", "red", "green", "blue", "cyan", "magenta", "yellow", "black"
	};

	private readonly string[] __badNames = new string[] {
		"Bob", "Tasha", "Sam",
	};

	private readonly string[] __misleadingNames = new string[] {
		"Simone", "Simeon", "Simba",
	};

	private bool lightsAreOn = true; // Timer's suspended if the lights are off.
	private bool isLive; // Currently accepting input, timer is being displayed, etc.
	private int answer; // Bitmask of valid buttons, or 0 for "Simon didn't say it"

	// Handles bomb being over / force solved by TP
	private bool isDisabled;

	string DebugValidButtons()
	{
		// Only for logging: returns valid buttons to press in string form
		switch (answer)
		{
			case 1: return "the first button";
			case 2: return "the second button";
			case 3: return "either the first or second button";
			case 4: return "the third button";
			case 5: return "either the first or third button";
			case 6: return "either the second or third button";
			case 7: return "any button except the fourth";
			case 8: return "the fourth button";
			case 9: return "either the first or fourth button";
			case 10: return "either the second or fourth button";
			case 11: return "any button except the third";
			case 12: return "either the third or fourth button";
			case 13: return "any button except the second";
			case 14: return "any button except the first";
			case 15: return "any button";
			default: return "nothing";
		}
	}



	// -----
	// Setting up questions and answers
	// -----

	// I debated not doing this. But in the end, there are a few phrases where
	// it stops being funny when they randomly show, and just downright sucks
	// in general to see them.
	string[] __badWords = new string[] {
		"\u0053\u0048\u0049\u0054",
		"\u0053\u0050\u0041\u005A",
		"\u0046\u0055\u0043\u004B",
		"\u0044\u0049\u0043\u004B",
		"\u0046\u0041\u0047",
		"\u0046\u0043\u004B",
	};

	bool BadWordInAssignment()
	{
		string[] labelReadings = new string[] {
			kpLabels[0].text + kpLabels[1].text + kpLabels[2].text + kpLabels[3].text,
			kpLabels[0].text + kpLabels[1].text + kpLabels[2].text,
			kpLabels[1].text + kpLabels[2].text + kpLabels[3].text,
		};

		foreach (string l in labelReadings)
		{
			if (Array.IndexOf(__badWords, l) != -1)
				return true;
		}
		return false;
	}

	char[] AssignRandomCharacters(string cPool)
	{
		char[] assigned;

		do
		{
			assigned = new char[4];

			List<char> pool = cPool.ToCharArray().ToList();
			for (int i = 0; i < 4; ++i)
			{
				int rPos = RNG.Range(0, pool.Count);
				kpLabels[i].text = pool[rPos].ToString();
				assigned[i] = pool[rPos];
				pool.RemoveAt(rPos);
			}
		} while (BadWordInAssignment());

		return assigned;
	}

	void AssignMultiCorrectCharacters(string cPoolA, string cPoolB, int secondBits)
	{
		do
		{
			// Note: Y is excluded so nobody asks.
			List<char> poolA = cPoolA.ToCharArray().ToList();
			List<char> poolB = cPoolB.ToCharArray().ToList();
			for (int i = 0; i < 4; ++i)
			{
				if ((secondBits & 1 << i) == 0)
				{
					int rPos = RNG.Range(0, poolA.Count);
					kpLabels[i].text = poolA[rPos].ToString();
					poolA.RemoveAt(rPos);
				}
				else
				{
					int rPos = RNG.Range(0, poolB.Count);
					kpLabels[i].text = poolB[rPos].ToString();
					poolB.RemoveAt(rPos);
				}
			}
		} while (BadWordInAssignment());
	}

	int[] AssignRandomColors()
	{
		int[] colorsAssigned = new int[4];
		List<int> pool = new List<int> { 0, 1, 1, 2, 2, 3, 3, 4, 5, 6, 7 };
		// Slightly skewed towards primary colors

		for (int i = 0; i < 4; ++i)
		{
			colorsAssigned[i] = pool[RNG.Range(0, pool.Count)];
			kpRends[i].material = kpMats[colorsAssigned[i]];
			pool.RemoveAll(v => v == colorsAssigned[i]);
		}

		return colorsAssigned;
	}

	string RandomCommand()
	{
		bool altText = (RNG.Range(0,2) == 1);
		int rCmd = RNG.Range(0, 63);
		string workStr;

		// Assign defaults
		for (int i = 0; i < 4; ++i)
		{
			kpRends[i].material = kpMats[0];
			kpLEDs[i].material = LEDOff;
			kpLabels[i].text = "";
		}

		if (rCmd < 8)
		{	// Push a specific letter (weight 8)
			AssignRandomCharacters("QWERTYUIOPASDFGHJKLZXCVBNM");
			answer = RNG.Range(0,4);

			if (altText)
			{
				workStr = String.Format("Is there a{1} {0}?",
					kpLabels[answer].text, "AEFHILMNORSX".Contains(kpLabels[answer].text) ? "n" : "");
			}
			else
				workStr = String.Format("Press {0}!", kpLabels[answer].text);

			answer = 1 << answer;
			return workStr;
		}
		else if (rCmd < 26)
		{	// Press any vowel/consonant (weight 10 & 8)
			answer = RNG.Range(1, 14);

			// "15 - answer" negates the first 15 bits (and only the first 15)
			// So nobody complains, Y is excluded from these.
			AssignMultiCorrectCharacters("QWRTPSDFGHJKLZXCVBNM", "AEIOU", (rCmd >= 18) ? 15 - answer : answer);
			if (rCmd >= 18)
				return altText ? "Consonant, please." : "Press a consonant!";
			return altText ? "Vowel, please." : "Press a vowel!";
		}
		else if (rCmd < 31)
		{	// Super basic mathematics (weight 5)
			string operation;
			char[] whatNumbers = AssignRandomCharacters("0123456789");

			answer = RNG.Range(0,4);
			switch (whatNumbers[answer])
			{
				case '0':  operation = altText ? "1 - 1" : "5 × 0"; break;
				case '1':  operation = altText ? "1 × 1" : "8 ÷ 8"; break;
				case '2':  operation = altText ? "1 + 1" : "6 ÷ 3"; break;
				case '3':  operation = altText ? "1 + 2" : "7 - 4"; break;
				case '4':  operation = altText ? "2 × 2" : "8 ÷ 2"; break;
				case '5':  operation = altText ? "3 + 2" : "5 × 1"; break;
				case '6':  operation = altText ? "3 + 3" : "3 × 2"; break;
				case '7':  operation = altText ? "2 + 5" : "7 - 0"; break;
				case '8':  operation = altText ? "6 + 2" : "8 ÷ 1"; break;
				case '9':  operation = altText ? "5 + 4" : "3 × 3"; break;
				default: operation = "NaN"; break;
			}
			workStr = String.Format("What's {0}?", operation);
			answer = 1 << answer;
			return workStr;
		}
		else if (rCmd < 45)
		{	// Odd or even (weight 7 & 7)
			answer = RNG.Range(1, 14);

			// "15 - answer" negates the first 15 bits (and only the first 15)
			AssignMultiCorrectCharacters("02468", "13579", (rCmd >= 38) ? 15 - answer : answer);
			if (rCmd >= 38)
				return altText ? "Any even numbers?" : "Press even!";
			return altText ? "Any odd numbers?" : "Press odd!";
		}
		else if (rCmd < 60)
		{	// Press color (weight 15)
			int[] whatColors = AssignRandomColors();
			answer = RNG.Range(0,4);
			workStr = String.Format("Press {0}!", __colors[whatColors[answer]]);
			answer = 1 << answer;
			return workStr;
		}
		else if (rCmd == 60)
		{	// Literally blank (rare)
			answer = 1 << RNG.Range(0,4);
			AssignMultiCorrectCharacters("QWERTYUIOPASDFGHJKLZXCVBNM", "    ", answer);
			return "Literally blank!";
		}

		// Fallback case: Press anything
		switch (RNG.Range(0,5))
		{
			case 0: case 1: AssignRandomCharacters("QWERTYUIOPASDFGHJKLZXCVBNM"); break;
			case 2:         AssignRandomCharacters("0123456789"); break;
			default:        AssignRandomColors(); break;
		}
		answer = 15;
		return "Press any button!";
	}


	// -----
	// Effects
	// -----

	private Color currentScreenColor = new Color(0.0f, 228.0f/256.0f, 0.0f, 1.0f);
	private readonly float[] __flicker = new float[] {
		0.048f, 0.032f, 0.097f, 0.049f, 0.023f, 0.021f, 0.064f, 0.058f,
		0.082f, 0.057f, 0.005f, 0.071f, 0.075f, 0.001f, 0.076f, 0.017f,
		0.067f, 0.063f, 0.028f, 0.036f, 0.077f, 0.009f, 0.086f, 0.086f,
		0.003f, 0.054f, 0.040f, 0.061f, 0.016f, 0.011f, 0.095f, 0.014f,
	};

	IEnumerator HandleLight()
	{
		int flickerIndex = 0;

		while (!isDisabled)
		{
			if (!lightsAreOn)
			{
				// Pause the timer if the lights turn off.
				if (isLive)
				{
					float time = bombModule.GetNeedyTimeRemaining();
					bombModule.SetNeedyTimeRemaining((float)Math.Round(time));
				}
				currentScreenColor.a = __flicker[flickerIndex++] * 2.0f;
			}
			else
				currentScreenColor.a = 1.0f - __flicker[flickerIndex++];

			mainScreen.color = currentScreenColor;
			flickerIndex %= __flicker.Length;
			yield return new WaitForSeconds(0.1f);
		}
	}


	// -----
	// Needy dirty work
	// -----

	// on the extremely rare case that the needy starts up again while doing cleanup, this lets us stop it.
	private Coroutine cleanupCoroutine;

	IEnumerator HandleNeedyCleanup(bool timedOut)
	{
		bombModule.HandlePass();
		isLive = false;

		mainScreen.text = "";
		if (!timedOut) // Delayed sound effect start
			yield return new WaitForSeconds(1f);

		bombAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.WireSequenceMechanism, bombModule.transform);
		yield break;
	}

	void OnNeedyActivation()
	{
		if (isDisabled)
		{
			bombModule.HandlePass();
			return;
		}

		int didSimonSayIt = RNG.Range(0, 100);
		string command = RandomCommand();

		if (cleanupCoroutine != null)
			StopCoroutine(cleanupCoroutine);

		Debug.LogFormat("[Simon Literally Says #{0}] ----------", thisLogID);

		if (didSimonSayIt < 50) // 50% chance Simon said it
			name = "Simon";
		else
		{
			answer = 0;
			if (didSimonSayIt < 85) // 35% chance nobody said it (just a printed command)
				name = "";
			else if (didSimonSayIt < 95) // 10% chance another person that isn't a name close to Simon said it
				name = __badNames[RNG.Range(0, __badNames.Length)];
			else                         //  5% chance a misleading name close to Simon said it
				name = __misleadingNames[RNG.Range(0, __misleadingNames.Length)];
		}

		if (name.Equals(""))
			mainScreen.text = command;
		else
			mainScreen.text = String.Format("{0} says:\n{1}", name, command);

		Debug.LogFormat("[Simon Literally Says #{0}] {2} says: {1}", thisLogID, command, name.Equals("") ? "Nobody" : name);

		if (answer != 0)
			Debug.LogFormat("[Simon Literally Says #{0}] Simon said it. I expect you to press {1}.", thisLogID, DebugValidButtons());			
		else
			Debug.LogFormat("[Simon Literally Says #{0}] Simon didn't say it. I expect you to let the timer expire.", thisLogID);

		for (int i = 0; i < kpAnims.Length; ++i)
			kpAnims[i].Play("KeypadAppear", 0, 0);

		isLive = true;

		// For players on Twitch Plays, be a little more generous with time due to stream delay and the command queue.
		if (TwitchPlaysActive)
			bombModule.SetNeedyTimeRemaining(30.0f);
	}

	void OnNeedyDeactivation()
	{
		isDisabled = true;
		if (isLive)
		{
			bombAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.WireSequenceMechanism, bombModule.transform);

			for (int i = 0; i < kpAnims.Length; ++i)
				kpAnims[i].Play("KeypadHide", 0, 0);

			bombModule.HandlePass();
		}
		isLive = false;
		mainScreen.text = "";
	}

	void OnTimerExpired()
	{
		if (answer != 0)
		{
			Debug.LogFormat("[Simon Literally Says #{0}] STRIKE: Timer expired on Simon's request.", thisLogID);		
			bombModule.HandleStrike();
		}
		else
			Debug.LogFormat("[Simon Literally Says #{0}] SAFE: Timer expired as expected.", thisLogID);

		for (int i = 0; i < kpAnims.Length; ++i)
			kpAnims[i].Play("KeypadHide", 0, 0);

		cleanupCoroutine = StartCoroutine(HandleNeedyCleanup(true));
	}

	void ButtonInteract(int button)
	{
		bool strike = true;

		if (!isLive)
			return;

		kpSelects[button].AddInteractionPunch(0.5f);
		bombAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, keypad[button].transform);

		if (answer == 0)
			Debug.LogFormat("[Simon Literally Says #{0}] STRIKE: Pressed button {1} instead of ignoring the request.", thisLogID, button + 1);
		else if ((answer & (1 << button)) == 0)
			Debug.LogFormat("[Simon Literally Says #{0}] STRIKE: Pressed an incorrect button, {1}.", thisLogID, button + 1);
		else
		{
			strike = false;
			Debug.LogFormat("[Simon Literally Says #{0}] SAFE: Pressed a correct button, {1}.", thisLogID, button + 1);
		}

		if (strike)
		{
			bombModule.HandleStrike();
			kpLEDs[button].material = LEDWrong;
		}
		else
			kpLEDs[button].material = LEDCorrect;

		for (int i = 0; i < kpAnims.Length; ++i)
			kpAnims[i].Play((i == button) ? "KeypadPress" : "KeypadDelayedHide", 0, 0);

		cleanupCoroutine = StartCoroutine(HandleNeedyCleanup(false));
	}

	void Awake()
	{
		thisLogID = ++globalLogID;

        bombModule.OnNeedyActivation += OnNeedyActivation;
        bombModule.OnNeedyDeactivation += OnNeedyDeactivation;
		bombModule.OnTimerExpired += OnTimerExpired;

		// Hook into bomb info, for lights.
		bombInfo.OnLightsChange += delegate(bool state) {
			if (lightsAreOn != state)
			{
				if (!state)
					Debug.LogFormat("[Simon Literally Says #{0}] Because the lights have turned off, the timer is now paused.", thisLogID);
				else
					Debug.LogFormat("[Simon Literally Says #{0}] Lights have turned back on, timer resumed.", thisLogID);
				lightsAreOn = state;
			}
		};

		kpSelects = new KMSelectable[keypad.Length];
		kpLabels = new TextMesh[keypad.Length];
		kpRends = new Renderer[keypad.Length];
		kpLEDs = new Renderer[keypad.Length];
		kpAnims = new Animator[keypad.Length];

		for (int i = 0; i < keypad.Length; ++i)
		{
			int j = i;
			GameObject tmp;

			kpSelects[i] = keypad[i].GetComponentInChildren<KMSelectable>();
			kpLabels[i] = keypad[i].GetComponentInChildren<TextMesh>();
			kpAnims[i] = keypad[i].GetComponent<Animator>();

			tmp = kpSelects[i].transform.Find("Key").gameObject;
			kpRends[i] = tmp.GetComponent<Renderer>();
			tmp = kpSelects[i].transform.Find("LED").gameObject;
			kpLEDs[i] = tmp.GetComponent<Renderer>();

			kpSelects[i].OnInteract += delegate() {
				ButtonInteract(j); 
				return false;
			};

			kpLabels[i].text = "";
		}

		mainScreen.text = "";
		mainScreen.color = currentScreenColor;

		StartCoroutine(HandleLight());
	}


	// -----
	// Twitch Plays Support
	// -----

#pragma warning disable 414
	private readonly string TwitchHelpMessage = @"Use '!{0} press 1' to press the button in that position.";
#pragma warning restore 414

	public IEnumerator ProcessTwitchCommand(string command)
	{
		Match mt;

        if ((mt = Regex.Match(command, @"^\s*(?:press|select)?\s*(\d)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).Success)
        {
        	int buttonToPress = Convert.ToInt32(mt.Groups[1].ToString());
        	if (buttonToPress >= 1 && buttonToPress <= 4)
        	{
        		yield return null;

        		// Be very clear in TP if a strike is earned for listening to the wrong person.
        		if (answer == 0)
					yield return "strikemessage pressing a button when Simon didn't tell you to.";

        		yield return new KMSelectable[] { kpSelects[buttonToPress - 1] };
        	}
        }
        yield break;
	}

	void TwitchHandleForcedSolve()
	{
		Debug.LogFormat("[Simon Literally Says #{0}] Needy is being disabled by Twitch Plays.", thisLogID);
		OnNeedyDeactivation();
	}
}
