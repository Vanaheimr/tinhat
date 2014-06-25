## tinhat ##

tinhat : crypto random for the truly paranoid

## why tinhat? ##

tinhat guarantees not to reduce the strength of your crypto random.  See below for more details on that.  But if your OS crypto random has in any way been undermined (for example, by a nefarious government agency, or simple incompetence) then tinhat has the ability to improve the strength of your crypto random.

## What is tinhat? ##

First, let's define a few terms.  In cryptography, we assume everything in the universe is actually deterministic.  The roll of dice, flip of a coin, shuffling of cards, and even radioactive decay and unpredictable quantum activities, all produce deterministic results from their respective physical processes.  In cryptography, "entropy" is the measure of unknown-ness with respect to another party.  Even if a coin flip is 100% deterministic, if I flip a coin and don't let you see it, I have gained 1 bit worth of entropy relative to you.  I can measure mouse movements on the screen, measure hard drive timings and network interrupts, and despite the fact that these are 100% deterministic, they provide effective entropy relative to any attacker who isn't also measuring the same sources, or exerting influence over them.

A cryptographically strong random source is simple:  It's anything which is indistinguishable from true random, because any and all would-be attackers do not have access to its entropy sources.

This leads us to a problem:  You can't flip a coin very fast.  Even in a computer, if you sample a whole bunch of entropy sources, the rate that you gain entropy is rather slow.  Pseudo random generators (PRNG's) are important, but equally important is knowing how and when to use them.  A PRNG uses cryptographic primitives (usually hash functions) to stretch a small amount of entropy into a larger amount of output data, exploiting the design characteristics of the primitives to produce output that remains indistinguishable from true random.  This fundamentally requires the implementation of the PRNG to remain ideal, free of any known flaws; but, as time goes on, weaknesses of the primitives are bound to be discovered.  It is recommended to use pure entropy for generation of long-lived keys, and to use PRNG output for  frequent operations such as short-lived keys and ephemeral key exchanges.

In tinhat, we provide two main classes.  The first main class, "TinHatRandom" draws entropy from available entropy sources, and mixes them together as described below, to eliminate or reduce the risk of any individual entropy source failing (being predictable by an attacker.)  TinHatRandom will never return more bytes than what it collected from entropy sources, which means it can be slow.  The second main class, "TinHatURandom" uses a PRNG, which is seeded by TinHatRandom, and has configurable thresholds to re-seed, and even refuse to return more bytes when drained too rapidly.  Both are designed to be cryptographically strong random, but TinHatRandom is slow while TinHatURandom is fast, and the cryptographic strength of TinHatURandom is dependent on the integrity of its internal PRNG.

## How does tinhat guarantee ... ? ##

Let's study the one-time pad.  The one-time pad (OTP) involves two parties meeting and generating a random sequence together.  This is a shared secret key, equal to or longer than the length of message.  Later, the message sender mixes character-by-character, the plaintext message with the random key, to generate the ciphertext and send the ciphertext.  The recipient is able to remove the random key to discover the plaintext.  This task cannot be performed without knowledge of the shared secret key.  When used correctly, the OTP provides perfect secrecy, because the ciphertext is indistinguishable from true random.  The really important characteristic here is:  *Even if the plaintext is a very predictable pattern, such as all repeating characters, or a pseudo-random pattern generated by a hostile government to undermine your security, when you mix that plaintext with a true random key, the output is indistinguishable from true random.*

I am specifically vague about "mixing" the plaintext with the key, because traditionally, OTP users would use add-without-carry (XOR), but that is not a requirement.  OTP users must use some reversible method (such as XOR) to mix the plaintext with the key, because they are interested in preserving the plaintext when the key is removed.  But if you have no interest in preserving the plaintext (tinhat is only interested in guaranteeing strong crypto random output) you could, for example, take a one-way hash of the plaintext, and XOR it with a hash of the key.  The output would still be indistinguishable from true random, but it would be impossible to determine anything about the original plaintext or random key (unless there is a fundamental flaw in your hash algorithm.)

Let's hypothetically suppose a nefarious government agency (or simple incompetence) has undermined your OS crypto random provider.  To you, the output looks random.  You have been told this output is cryptographically strong random.  But the hypothetical nefarious agency (or just some kid on the internet) has some secret way of guessing the supposedly random values generated on your system.  Let's suppose you have some additional entropy sources - maybe user mouse movements, user keyboard entry, timings of hard drive events, process or thread scheduling contention events, or some other hardware entropy sources...  But you're not completely sure that *any* of them can be trusted as a source of cryptographic random.  What you can do to improve your security is to pull supposedly random data from all these sources, mix them all together, and as long as *any* of them is effectively random (as a key to OTP), then your output is effectively random (as a OTP ciphertext).

The only way to undermine this technique, is for one supposed entropy source to actively predict the output of another supposed entropy source, and try to (or accidentally) interfere with the other, or perform some sort of logging, recording the other for post-analysis.  Checking for the existence of trojans and/or entropy logging is beyond the scope of this project, although, there exist sources on the internet (reputable?) that state certain OSes record and archive all keys generated by the OS.  This is an exercise left for the reader.  A very likely use for tinhat is to feed random data into a non-OS key generator, such as BouncyCastle, which is open source and surely does not record either the random data or the keys generated.  The suspicion remains at the OS level - Is it possible for the OS to sample and record the random data it produces, and also the entropy sources that are used by tinhat to generate random data for an alternate key generator?  Of course it is, but the level of mal-intent and targeted focus are much greater to do this attack, than to come up with some plausible excuse to record all the keys generated by the OS.  So the prospect of OS recording all keys seems much more plausible, and much more deniable by the OS manufacturer, than the prospect of the OS recording all your random data and entropy sources.  For example, the OS manufacturer could say, "We need to record and archive all the keys in order to ensure they're never repeated."  It would sound plausible and reasonable to a lot of people who don't know any better, and very likely get the OS manufacturer out of legal trouble or other trouble, despite being obviously wrong and irresponsible in the eyes of any responsible cryptanalyst.

Getting back to the subject at hand, we are willfully neglecting the possibility of the OS actively recording your entropy sources for post-analysis.  The remaining way to undermine our technique of mixing random sources to produce a more secure random source, is for some supposedly random source to maliciously or accidentally be identical or related to another.  We analyze as follows:

Assume two supposedly crypto random sources actually are related to each other in some way.  Call them Source A and Source B.  For example, suppose Source B extracts entropy from mouse movements, and suppose Source A knows its output will be XOR'd with the output of Source B, and Source A also watches the mouse movements, intentionally trying to generate output that will undermine the cryptographic strength of Source B when the two outputs are mixed.  Source A has precisely three strategies it can choose from:  It can generate the same output as B, generate different but related output, or completely unrelated.  We don't have to consider the case of unrelated output - as it cannot undermine the integrity of Source B's output.  We can trivially detect and correct the case of identical output:  Just compare the outputs, and if they're equal, discard one of them.  So the only non-trivial case is the case of different but related, which we explore in more detail as follows:

The design requirements of a cryptographically secure hash function requires that its output be indistinguishable from random, and that any two different (but possibly related) inputs produce outputs that have no known relation to each other or the inputs.  Any violation of these requirements would result in a "distinguishing attack" against the hash function, and as such, the hash function would lose its strength of "collision resistance," and would henceforth be considered broken, or insecure.

Let's take a hash of A, and call the result A'.  Unless there is a fundamental flaw in the hash function, the relationship here is that A' is apparently random, revealing nothing about A.  Similarly, let B be possibly related, but different from A.  Take a hash, with possibly a different hash function, of B as B'.  This is yet again, apparently random, revealing nothing about B.  If we may assume the hash functions remain cryptographically sound, it is then impossible to manipulate A, different from B, such that A' will have any known relation to B'. 

We are necessarily placing a lot of trust into the hash function, in the sense that no distinguishing attacks are known, and the cryptography community still considers the hash function to be "unbroken."  The moderately paranoid might use a hash function that's publicly considered "secure," such as one of the SHA family.  The extremely paranoid might suspect somebody out there has secret knowledge of a flaw in the SHA family.  To address this concern, let us construct a hash function which is just a wrapper around other hash functions.  Even if a weakness is known for some of the internal hash functions, the attacker still cannot manipulate the input in any way to have a controllable effect on the output, as long as *any* of the internal hash functions remains unbroken.

Proceduralizing all of the above:

* Let there be an arbitrary number of supposedly crypto random sources, or entropy sources, named A, B, C, ...  Let us distrust their entropic integrity or usefulness as cryptographic entropy sources.
* Choose a secure crypto hash function, or a suite of hash functions, with output size m.
* When a user requests n bytes of random data, repeat this process for every m bytes requested:
    * Read m bytes from each of the sources A, B, C, ...
    * Compare each of them against each other for equality, and eliminate any duplicates.
    * Generate hashes A', B', C', ...  The hash functions used for each may be different from each other, and/or may be chained or mixed, provided they are all considered unbroken cryptographic hash functions, with the same output size m.
    * Combine the hashes A', B', C', ... via XOR, and return the result to the user, up to the number of bytes requested.

This is the architecture behind TinHatRandom.  By comparison, TinHatURandom is a simple wrapper around a PRNG, which uses TinHatRandom for seed material.

## Download ##

It is generally recommended to use NuGet to add the library to your project/solution.

- Visual Studio: In your project, right-click References, and Manage NuGet Packages. Search for TinHat, and install.
- Xamarin Studio / MonoDevelop: If you don't already have a NuGet package manager, install it from <https://github.com/mrward/monodevelop-nuget-addin>.  And then right-click References, and Manage NuGet Packages.  Search for TinHat, and install.

Tinhat source code is available at <https://github.com/rahvee/tinhat>

## License ##

Tinhat Random is distributed under the MIT license.  Details here:  <https://raw.githubusercontent.com/rahvee/tinhat/master/LICENSE>

## Documentation and API ##

If you want simple usage, here is an example.

    static void Main(string[] args)
    {
        tinhat.StartEarly.StartFillingEntropyPools();  // Start gathering entropy as early as possible
    
        var randomBytes = new byte[32];
    
        // Only use TinHatRandom for long-lived keys.  Use TinHatURandom for everything else.
        // On my system, TinHatRandom generated about 15-60 KiB/sec
        // default constructor uses SystemRNGCryptoServiceProvider/SHA256, ThreadedSeedGeneratorRNG/SHA256/RipeMD256Digest
        using (var rng = new tinhat.TinHatRandom())
        {
            rng.GetBytes(randomBytes);
        }
    
        // Use TinHatURandom for general cryptographic random purposes.
        // On my system, TinHatURandom generated about 2-8 MiB/sec
        // default constructor uses SystemRNGCryptoServiceProvider/SHA256, ThreadedSeedGeneratorRNG/SHA256/RipeMD256Digest
        using (var rng = new tinhat.TinHatURandom())
        {
            rng.GetBytes(randomBytes);
        }
    }

If you want to manually specify the hash algorithms and entropy sources, please consult the documentation below.

For windows users, we recommend downloading the chm file (compressed html, displays natively in your windows help dialog by just double-clicking the chm file).  <https://github.com/rahvee/tinhat/raw/master/Documentation/tinhat.chm>

The html when viewed in a web browser, doesn't render quite as nicely, but here it is, for anyone who doesn't want or can't use the chm.  <https://www.tinhatrandom.org/API>

## Support ##

Please send email to <tinhatrandom-discuss@tinhatrandom.org>
