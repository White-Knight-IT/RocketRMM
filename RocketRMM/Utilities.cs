using Microsoft.IdentityModel.Tokens;
using RocketRMM.Data.Logging;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using CliWrap;
using System.Reflection.Metadata;

namespace RocketRMM
{
    internal class Utilities
    {
        /// <summary>
        /// Allows writing to the console with an optional colour
        /// </summary>
        /// <param name="line">The string to write to the console</param>
        /// <param name="colour">The foreground colour of the text written to console</param>
        /// <param name="addNewLine">Add a newline (\n) to the start of the line</param>
        public static void ConsoleColourWriteLine(string line, ConsoleColor colour = ConsoleColor.White, bool addNewLine=true)
        {
            Console.ForegroundColor = colour;
            if(addNewLine)
            {
                line=$"\n{line}";
            }
            Console.WriteLine(line);
        }

        /// <summary>
        /// Takes raw JSON and a designated type and it converts the JSON into a list of objects of the given type
        /// </summary>
        /// <typeparam name="type">Will parse into a list of objects of this type</typeparam>
        /// <param name="rawJson"></param>
        /// <returns>List of objects defined by given type</returns>
        internal static List<type> ParseJson<type>(List<JsonElement> rawJson)
        {
            List<type> objectArrayList = [];

            foreach (JsonElement je in rawJson)
            {
                objectArrayList.Add(JsonSerializer.Deserialize<type>(je, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true, MaxDepth = 64 }));
            }

            return objectArrayList;
        }

        /// <summary>
        /// Submit any JSON object/s to write to file
        /// </summary>
        /// <typeparam name="type">type of JSON object/s e.g. Tenant or List<Tenant></typeparam>
        /// <param name="json">JSON object/s to serialize to file</param>
        /// <param name="filePath">File path, use "" to avoid writing to file and just get the string</param>
        ///<returns>The string that was written to file</returns>
        internal static async Task<string> WriteJsonToFile<type>(object json, string filePath, bool encrypt = false, byte[]? key = null, bool newLines=false)
        {
            string jsonString = JsonSerializer.Serialize((type)json);

            if (encrypt)
            {
                jsonString = await Crypto.AesEncrypt(jsonString, key, newLines);
            }

            if (!filePath.IsNullOrEmpty())
            {
                File.WriteAllText(filePath, jsonString);
            }

            return jsonString;
        }

        /// <summary>
        /// Return file contents as JSON object of specified type
        /// </summary>
        /// <typeparam name="type">Type of our JSON object to make</typeparam>
        /// <param name="filePath">Path to our file containing JSON</param>
        /// <returns>JSON object of specified type</returns>
        internal static async Task<type> ReadJsonFromFile<type>(string filePath, bool decrypt = false, byte[]? key = null)
        {
            string jsonString = File.ReadAllText(filePath);

            if (decrypt)
            {
                jsonString = await Crypto.AesDecrypt(jsonString, key);
            }

            return JsonSerializer.Deserialize<type>(jsonString);
        }

        /// <summary>
        /// Evaluates JSON boolean and treats null as false
        /// </summary>
        /// <param name="property">JSON boolean to check</param>
        /// <returns>true/false</returns>
        internal static bool NullIsFalse(JsonElement property)
        {
            try
            {
                return property.GetBoolean();
            }
            catch
            {

            }

            return false;
        }

        /// <summary>
        /// Decodes a base64url string into a byte array
        /// </summary>
        /// <param name="arg">string to convert to bytes</param>
        /// <returns>byte[] containing decoded bytes</returns>
        /// <exception cref="Exception">Illegal base64url string</exception>
        internal static byte[] Base64UrlDecode(string arg)
        {
            string s = arg;
            s = s.Replace('-', '+'); // 62nd char of encoding
            s = s.Replace('_', '/'); // 63rd char of encoding
            switch (s.Length % 4) // Pad with trailing '='s
            {
                case 0: break; // No pad chars in this case
                case 2: s += "=="; break; // Two pad chars
                case 3: s += "="; break; // One pad char
                default:

                    _ = LogsDbThreadSafeCoordinator.ThreadSafeAdd(new LogEntry()
                    {
                        Message = $"Illegal base64url string: {arg}",
                        Severity = "Error",
                        API = "Base64UrlDecode"
                    });

                    throw new Exception($"Illegal base64url string: {arg}");
            }
            return Convert.FromBase64String(s); // Standard base64 decoder
        }

        /// <summary>
        /// Encodes a byte array into a Base64 encoded string
        /// </summary>
        /// <param name="bytes">bytes to encode</param>
        /// <param name="newLine">If set true adds a new line every 74 chars</param>
        /// <returns>Base64 encoded string</returns>
        internal static async Task<string> Base64Encode(byte[] bytes, bool newLines = false)
        {
            Task<string> task = new(() =>
            {
                if (!newLines)
                {
                    return Convert.ToBase64String(bytes);
                }

                return Convert.ToBase64String(bytes, Base64FormattingOptions.InsertLineBreaks);
            });

            task.Start();

            return await task;
        }

        /// <summary>
        /// Decodes a Base64 encoded string into a byte array
        /// </summary>
        /// <param name="text"></param>
        /// <returns>byte array containing bytes of string</returns>
        internal static async Task<byte[]> Base64Decode(string text)
        {
            Task<byte[]> task = new(() =>
            {
                return Convert.FromBase64String(text);

            });

            task.Start();

            return await task;
        }

        /// <summary>
        /// Gets username from the token within the supplied httpcontext
        /// </summary>
        /// <param name="context">httpcontext containing the token</param>
        /// <returns>The username which made the http request</returns>
        internal static async Task<string> UsernameParse(HttpContext context)
        {
            try
            {
                return context.User.Claims.First(x => x.Type.ToLower().Equals("preferred_username")).Value;
            }
            catch
            {
                return "Illegal Alien";
            }
        }

        internal static class Crypto
        {
            // From BIP39 english wordlist
            internal static readonly List<string> WordDictionary = ["abandon", "ability", "able", "about", "above", "absent", "absorb", "abstract", "absurd", "abuse", "access",
                "accident", "account", "accuse", "achieve", "acid", "acoustic", "acquire", "across", "act", "action", "actor", "actress", "actual", "adapt", "add", "addict",
                "address", "adjust", "admit", "adult", "advance", "advice", "aerobic", "affair", "afford", "afraid", "again", "age", "agent", "agree", "ahead", "aim", "air", 
                "airport", "aisle", "alarm", "album", "alcohol", "alert", "alien", "all", "alley", "allow", "almost", "alone", "alpha", "already", "also", "alter", "always", 
                "amateur", "amazing", "among", "amount", "amused", "analyst", "anchor", "ancient", "anger", "angle", "angry", "animal", "ankle", "announce", "annual", "another", 
                "answer", "antenna", "antique", "anxiety", "any", "apart", "apology", "appear", "apple", "approve", "april", "arch", "arctic", "area", "arena", "argue", "arm",
                "armed", "armor", "army", "around", "arrange", "arrest", "arrive", "arrow", "art", "artefact", "artist", "artwork", "ask", "aspect", "assault", "asset", "assist",
                "assume", "asthma", "athlete", "atom", "attack", "attend", "attitude", "attract", "auction", "audit", "august", "aunt", "author", "auto", "autumn", "average",
                "avocado", "avoid", "awake", "aware", "away", "awesome", "awful", "awkward", "axis", "baby", "bachelor", "bacon", "badge", "bag", "balance", "balcony", "ball",
                "bamboo", "banana", "banner", "bar", "barely", "bargain", "barrel", "base", "basic", "basket", "battle", "beach", "bean", "beauty", "because", "become", "beef",
                "before", "begin", "behave", "behind", "believe", "below", "belt", "bench", "benefit", "best", "betray", "better", "between", "beyond", "bicycle", "bid", "bike",
                "bind", "biology", "bird", "birth", "bitter", "black", "blade", "blame", "blanket", "blast", "bleak", "bless", "blind", "blood", "blossom", "blouse", "blue",
                "blur", "blush", "board", "boat", "body", "boil", "bomb", "bone", "bonus", "book", "boost", "border", "boring", "borrow", "boss", "bottom", "bounce", "box",
                "boy", "bracket", "brain", "brand", "brass", "brave", "bread", "breeze", "brick", "bridge", "brief", "bright", "bring", "brisk", "broccoli", "broken", "bronze",
                "broom", "brother", "brown", "brush", "bubble", "buddy", "budget", "buffalo", "build", "bulb", "bulk", "bullet", "bundle", "bunker", "burden", "burger", "burst",
                "bus", "business", "busy", "butter", "buyer", "buzz", "cabbage", "cabin", "cable", "cactus", "cage", "cake", "call", "calm", "camera", "camp", "can", "canal",
                "cancel", "candy", "cannon", "canoe", "canvas", "canyon", "capable", "capital", "captain", "car", "carbon", "card", "cargo", "carpet", "carry", "cart", "case",
                "cash", "casino", "castle", "casual", "cat", "catalog", "catch", "category", "cattle", "caught", "cause", "caution", "cave", "ceiling", "celery", "cement", "census",
                "century", "cereal", "certain", "chair", "chalk", "champion", "change", "chaos", "chapter", "charge", "chase", "chat", "cheap", "check", "cheese", "chef", "cherry",
                "chest", "chicken", "chief", "child", "chimney", "choice", "choose", "chronic", "chuckle", "chunk", "churn", "cigar", "cinnamon", "circle", "citizen", "city",
                "civil", "claim", "clap", "clarify", "claw", "clay", "clean", "clerk", "clever", "click", "client", "cliff", "climb", "clinic", "clip", "clock", "clog", "close",
                "cloth", "cloud", "clown", "club", "clump", "cluster", "clutch", "coach", "coast", "coconut", "code", "coffee", "coil", "coin", "collect", "color", "column",
                "combine", "come", "comfort", "comic", "common", "company", "concert", "conduct", "confirm", "congress", "connect", "consider", "control", "convince", "cook",
                "cool", "copper", "copy", "coral", "core", "corn", "correct", "cost", "cotton", "couch", "country", "couple", "course", "cousin", "cover", "coyote", "crack", 
                "cradle", "craft", "cram", "crane", "crash", "crater", "crawl", "crazy", "cream", "credit", "creek", "crew", "cricket", "crime", "crisp", "critic", "crop", 
                "cross", "crouch", "crowd", "crucial", "cruel", "cruise", "crumble", "crunch", "crush", "cry", "crystal", "cube", "culture", "cup", "cupboard", "curious", 
                "current", "curtain", "curve", "cushion", "custom", "cute", "cycle", "dad", "damage", "damp", "dance", "danger", "daring", "dash", "daughter", "dawn", "day", "deal",
                "debate", "debris", "decade", "december", "decide", "decline", "decorate", "decrease", "deer", "defense", "define", "defy", "degree", "delay", "deliver", "demand",
                "demise", "denial", "dentist", "deny", "depart", "depend", "deposit", "depth", "deputy", "derive", "describe", "desert", "design", "desk", "despair", "destroy",
                "detail", "detect", "develop", "device", "devote", "diagram", "dial", "diamond", "diary", "dice", "diesel", "diet", "differ", "digital", "dignity", "dilemma",
                "dinner", "dinosaur", "direct", "dirt", "disagree", "discover", "disease", "dish", "dismiss", "disorder", "display", "distance", "divert", "divide", "divorce",
                "dizzy", "doctor", "document", "dog", "doll", "dolphin", "domain", "donate", "donkey", "donor", "door", "dose", "double", "dove", "draft", "dragon", "drama",
                "drastic", "draw", "dream", "dress", "drift", "drill", "drink", "drip", "drive", "drop", "drum", "dry", "duck", "dumb", "dune", "during", "dust", "dutch", "duty",
                "dwarf", "dynamic", "eager", "eagle", "early", "earn", "earth", "easily", "east", "easy", "echo", "ecology", "economy", "edge", "edit", "educate", "effort", "egg",
                "eight", "either", "elbow", "elder", "electric", "elegant", "element", "elephant", "elevator", "elite", "else", "embark", "embody", "embrace", "emerge", "emotion",
                "employ", "empower", "empty", "enable", "enact", "end", "endless", "endorse", "enemy", "energy", "enforce", "engage", "engine", "enhance", "enjoy", "enlist", "enough",
                "enrich", "enroll", "ensure", "enter", "entire", "entry", "envelope", "episode", "equal", "equip", "era", "erase", "erode", "erosion", "error", "erupt", "escape",
                "essay", "essence", "estate", "eternal", "ethics", "evidence", "evil", "evoke", "evolve", "exact", "example", "excess", "exchange", "excite", "exclude", "excuse",
                "execute", "exercise", "exhaust", "exhibit", "exile", "exist", "exit", "exotic", "expand", "expect", "expire", "explain", "expose", "express", "extend", "extra", "eye",
                "eyebrow", "fabric", "face", "faculty", "fade", "faint", "faith", "fall", "false", "fame", "family", "famous", "fan", "fancy", "fantasy", "farm", "fashion", "fat",
                "fatal", "father", "fatigue", "fault", "favorite", "feature", "february", "federal", "fee", "feed", "feel", "female", "fence", "festival", "fetch", "fever", "few",
                "fiber", "fiction", "field", "figure", "file", "film", "filter", "final", "find", "fine", "finger", "finish", "fire", "firm", "first", "fiscal", "fish", "fit", "fitness",
                "fix", "flag", "flame", "flash", "flat", "flavor", "flee", "flight", "flip", "float", "flock", "floor", "flower", "fluid", "flush", "fly", "foam", "focus", "fog", "foil",
                "fold", "follow", "food", "foot", "force", "forest", "forget", "fork", "fortune", "forum", "forward", "fossil", "foster", "found", "fox", "fragile", "frame", "frequent",
                "fresh", "friend", "fringe", "frog", "front", "frost", "frown", "frozen", "fruit", "fuel", "fun", "funny", "furnace", "fury", "future", "gadget", "gain", "galaxy",
                "gallery", "game", "gap", "garage", "garbage", "garden", "garlic", "garment", "gas", "gasp", "gate", "gather", "gauge", "gaze", "general", "genius", "genre", "gentle",
                "genuine", "gesture", "ghost", "giant", "gift", "giggle", "ginger", "giraffe", "girl", "give", "glad", "glance", "glare", "glass", "glide", "glimpse", "globe", "gloom",
                "glory", "glove", "glow", "glue", "goat", "goddess", "gold", "good", "goose", "gorilla", "gospel", "gossip", "govern", "gown", "grab", "grace", "grain", "grant", "grape",
                "grass", "gravity", "great", "green", "grid", "grief", "grit", "grocery", "group", "grow", "grunt", "guard", "guess", "guide", "guilt", "guitar", "gun", "gym", "habit",
                "hair", "half", "hammer", "hamster", "hand", "happy", "harbor", "hard", "harsh", "harvest", "hat", "have", "hawk", "hazard", "head", "health", "heart", "heavy", "hedgehog",
                "height", "hello", "helmet", "help", "hen", "hero", "hidden", "high", "hill", "hint", "hip", "hire", "history", "hobby", "hockey", "hold", "hole", "holiday", "hollow",
                "home", "honey", "hood", "hope", "horn", "horror", "horse", "hospital", "host", "hotel", "hour", "hover", "hub", "huge", "human", "humble", "humor", "hundred", "hungry",
                "hunt", "hurdle", "hurry", "hurt", "husband", "hybrid", "ice", "icon", "idea", "identify", "idle", "ignore", "ill", "illegal", "illness", "image", "imitate", "immense", 
                "immune", "impact", "impose", "improve", "impulse", "inch", "include", "income", "increase", "index", "indicate", "indoor", "industry", "infant", "inflict", "inform",
                "inhale", "inherit", "initial", "inject", "injury", "inmate", "inner", "innocent", "input", "inquiry", "insane", "insect", "inside", "inspire", "install", "intact",
                "interest", "into", "invest", "invite", "involve", "iron", "island", "isolate", "issue", "item", "ivory", "jacket", "jaguar", "jar", "jazz", "jealous", "jeans", "jelly",
                "jewel", "job", "join", "joke", "journey", "joy", "judge", "juice", "jump", "jungle", "junior", "junk", "just", "kangaroo", "keen", "keep", "ketchup", "key", "kick", 
                "kid", "kidney", "kind", "kingdom", "kiss", "kit", "kitchen", "kite", "kitten", "kiwi", "knee", "knife", "knock", "know", "lab", "label", "labor", "ladder", "lady",
                "lake", "lamp", "language", "laptop", "large", "later", "latin", "laugh", "laundry", "lava", "law", "lawn", "lawsuit", "layer", "lazy", "leader", "leaf", "learn",
                "leave", "lecture", "left", "leg", "legal", "legend", "leisure", "lemon", "lend", "length", "lens", "leopard", "lesson", "letter", "level", "liar", "liberty", "library",
                "license", "life", "lift", "light", "like", "limb", "limit", "link", "lion", "liquid", "list", "little", "live", "lizard", "load", "loan", "lobster", "local", "lock",
                "logic", "lonely", "long", "loop", "lottery", "loud", "lounge", "love", "loyal", "lucky", "luggage", "lumber", "lunar", "lunch", "luxury", "lyrics", "machine", "mad",
                "magic", "magnet", "maid", "mail", "main", "major", "make", "mammal", "man", "manage", "mandate", "mango", "mansion", "manual", "maple", "marble", "march", "margin",
                "marine", "market", "marriage", "mask", "mass", "master", "match", "material", "math", "matrix", "matter", "maximum", "maze", "meadow", "mean", "measure", "meat",
                "mechanic", "medal", "media", "melody", "melt", "member", "memory", "mention", "menu", "mercy", "merge", "merit", "merry", "mesh", "message", "metal", "method", "middle",
                "midnight", "milk", "million", "mimic", "mind", "minimum", "minor", "minute", "miracle", "mirror", "misery", "miss", "mistake", "mix", "mixed", "mixture", "mobile", 
                "model", "modify", "mom", "moment", "monitor", "monkey", "monster", "month", "moon", "moral", "more", "morning", "mosquito", "mother", "motion", "motor", "mountain", 
                "mouse", "move", "movie", "much", "muffin", "mule", "multiply", "muscle", "museum", "mushroom", "music", "must", "mutual", "myself", "mystery", "myth", "naive", "name",
                "napkin", "narrow", "nasty", "nation", "nature", "near", "neck", "need", "negative", "neglect", "neither", "nephew", "nerve", "nest", "net", "network", "neutral", "never",
                "news", "next", "nice", "night", "noble", "noise", "nominee", "noodle", "normal", "north", "nose", "notable", "note", "nothing", "notice", "novel", "now", "nuclear",
                "number", "nurse", "nut", "oak", "obey", "object", "oblige", "obscure", "observe", "obtain", "obvious", "occur", "ocean", "october", "odor", "off", "offer", "office", 
                "often", "oil", "okay", "old", "olive", "olympic", "omit", "once", "one", "onion", "online", "only", "open", "opera", "opinion", "oppose", "option", "orange", "orbit", 
                "orchard", "order", "ordinary", "organ", "orient", "original", "orphan", "ostrich", "other", "outdoor", "outer", "output", "outside", "oval", "oven", "over", "own", "owner",
                "oxygen", "oyster", "ozone", "pact", "paddle", "page", "pair", "palace", "palm", "panda", "panel", "panic", "panther", "paper", "parade", "parent", "park", "parrot", "party",
                "pass", "patch", "path", "patient", "patrol", "pattern", "pause", "pave", "payment", "peace", "peanut", "pear", "peasant", "pelican", "pen", "penalty", "pencil", "people",
                "pepper", "perfect", "permit", "person", "pet", "phone", "photo", "phrase", "physical", "piano", "picnic", "picture", "piece", "pig", "pigeon", "pill", "pilot", "pink",
                "pioneer", "pipe", "pistol", "pitch", "pizza", "place", "planet", "plastic", "plate", "play", "please", "pledge", "pluck", "plug", "plunge", "poem", "poet", "point", "polar",
                "pole", "police", "pond", "pony", "pool", "popular", "portion", "position", "possible", "post", "potato", "pottery", "poverty", "powder", "power", "practice", "praise",
                "predict", "prefer", "prepare", "present", "pretty", "prevent", "price", "pride", "primary", "print", "priority", "prison", "private", "prize", "problem", "process",
                "produce", "profit", "program", "project", "promote", "proof", "property", "prosper", "protect", "proud", "provide", "public", "pudding", "pull", "pulp", "pulse", "pumpkin",
                "punch", "pupil", "puppy", "purchase", "purity", "purpose", "purse", "push", "put", "puzzle", "pyramid", "quality", "quantum", "quarter", "question", "quick", "quit", "quiz",
                "quote", "rabbit", "raccoon", "race", "rack", "radar", "radio", "rail", "rain", "raise", "rally", "ramp", "ranch", "random", "range", "rapid", "rare", "rate", "rather",
                "raven", "raw", "razor", "ready", "real", "reason", "rebel", "rebuild", "recall", "receive", "recipe", "record", "recycle", "reduce", "reflect", "reform", "refuse", "region",
                "regret", "regular", "reject", "relax", "release", "relief", "rely", "remain", "remember", "remind", "remove", "render", "renew", "rent", "reopen", "repair", "repeat", "replace",
                "report", "require", "rescue", "resemble", "resist", "resource", "response", "result", "retire", "retreat", "return", "reunion", "reveal", "review", "reward", "rhythm", "rib",
                "ribbon", "rice", "rich", "ride", "ridge", "rifle", "right", "rigid", "ring", "riot", "ripple", "risk", "ritual", "rival", "river", "road", "roast", "robot", "robust", "rocket",
                "romance", "roof", "rookie", "room", "rose", "rotate", "rough", "round", "route", "royal", "rubber", "rude", "rug", "rule", "run", "runway", "rural", "sad", "saddle", "sadness",
                "safe", "sail", "salad", "salmon", "salon", "salt", "salute", "same", "sample", "sand", "satisfy", "satoshi", "sauce", "sausage", "save", "say", "scale", "scan", "scare",
                "scatter", "scene", "scheme", "school", "science", "scissors", "scorpion", "scout", "scrap", "screen", "script", "scrub", "sea", "search", "season", "seat", "second", "secret",
                "section", "security", "seed", "seek", "segment", "select", "sell", "seminar", "senior", "sense", "sentence", "series", "service", "session", "settle", "setup", "seven", "shadow",
                "shaft", "shallow", "share", "shed", "shell", "sheriff", "shield", "shift", "shine", "ship", "shiver", "shock", "shoe", "shoot", "shop", "short", "shoulder", "shove", "shrimp",
                "shrug", "shuffle", "shy", "sibling", "sick", "side", "siege", "sight", "sign", "silent", "silk", "silly", "silver", "similar", "simple", "since", "sing", "siren", "sister",
                "situate", "six", "size", "skate", "sketch", "ski", "skill", "skin", "skirt", "skull", "slab", "slam", "sleep", "slender", "slice", "slide", "slight", "slim", "slogan", "slot",
                "slow", "slush", "small", "smart", "smile", "smoke", "smooth", "snack", "snake", "snap", "sniff", "snow", "soap", "soccer", "social", "sock", "soda", "soft", "solar", "soldier",
                "solid", "solution", "solve", "someone", "song", "soon", "sorry", "sort", "soul", "sound", "soup", "source", "south", "space", "spare", "spatial", "spawn", "speak", "special",
                "speed", "spell", "spend", "sphere", "spice", "spider", "spike", "spin", "spirit", "split", "spoil", "sponsor", "spoon", "sport", "spot", "spray", "spread", "spring", "spy",
                "square", "squeeze", "squirrel", "stable", "stadium", "staff", "stage", "stairs", "stamp", "stand", "start", "state", "stay", "steak", "steel", "stem", "step", "stereo", "stick",
                "still", "sting", "stock", "stomach", "stone", "stool", "story", "stove", "strategy", "street", "strike", "strong", "struggle", "student", "stuff", "stumble", "style", "subject",
                "submit", "subway", "success", "such", "sudden", "suffer", "sugar", "suggest", "suit", "summer", "sun", "sunny", "sunset", "super", "supply", "supreme", "sure", "surface", "surge",
                "surprise", "surround", "survey", "suspect", "sustain", "swallow", "swamp", "swap", "swarm", "swear", "sweet", "swift", "swim", "swing", "switch", "sword", "symbol", "symptom",
                "syrup", "system", "table", "tackle", "tag", "tail", "talent", "talk", "tank", "tape", "target", "task", "taste", "tattoo", "taxi", "teach", "team", "tell", "ten", "tenant",
                "tennis", "tent", "term", "test", "text", "thank", "that", "theme", "then", "theory", "there", "they", "thing", "this", "thought", "three", "thrive", "throw", "thumb", "thunder",
                "ticket", "tide", "tiger", "tilt", "timber", "time", "tiny", "tip", "tired", "tissue", "title", "toast", "tobacco", "today", "toddler", "toe", "together", "toilet", "token",
                "tomato", "tomorrow", "tone", "tongue", "tonight", "tool", "tooth", "top", "topic", "topple", "torch", "tornado", "tortoise", "toss", "total", "tourist", "toward", "tower", "town",
                "toy", "track", "trade", "traffic", "tragic", "train", "transfer", "trap", "trash", "travel", "tray", "treat", "tree", "trend", "trial", "tribe", "trick", "trigger", "trim", "trip",
                "trophy", "trouble", "truck", "true", "truly", "trumpet", "trust", "truth", "try", "tube", "tuition", "tumble", "tuna", "tunnel", "turkey", "turn", "turtle", "twelve", "twenty",
                "twice", "twin", "twist", "two", "type", "typical", "ugly", "umbrella", "unable", "unaware", "uncle", "uncover", "under", "undo", "unfair", "unfold", "unhappy", "uniform",
                "unique", "unit", "universe", "unknown", "unlock", "until", "unusual", "unveil", "update", "upgrade", "uphold", "upon", "upper", "upset", "urban", "urge", "usage", "use", "used",
                "useful", "useless", "usual", "utility", "vacant", "vacuum", "vague", "valid", "valley", "valve", "van", "vanish", "vapor", "various", "vast", "vault", "vehicle", "velvet",
                "vendor", "venture", "venue", "verb", "verify", "version", "very", "vessel", "veteran", "viable", "vibrant", "vicious", "victory", "video", "view", "village", "vintage", "violin",
                "virtual", "virus", "visa", "visit", "visual", "vital", "vivid", "vocal", "voice", "void", "volcano", "volume", "vote", "voyage", "wage", "wagon", "wait", "walk", "wall", "walnut",
                "want", "warfare", "warm", "warrior", "wash", "wasp", "waste", "water", "wave", "way", "wealth", "weapon", "wear", "weasel", "weather", "web", "wedding", "weekend", "weird",
                "welcome", "west", "wet", "whale", "what", "wheat", "wheel", "when", "where", "whip", "whisper", "wide", "width", "wife", "wild", "will", "win", "window", "wine", "wing", "wink",
                "winner", "winter", "wire", "wisdom", "wise", "wish", "witness", "wolf", "woman", "wonder", "wood", "wool", "word", "work", "world", "worry", "worth", "wrap", "wreck", "wrestle",
                "wrist", "write", "wrong", "yard", "year", "yellow", "you", "young", "youth", "zebra", "zero", "zone", "zoo"];

            /// <summary>
            /// Returns a base64 encoded string consisting of 4098 crypto random bytes (4kb), this is cryptosafe random
            /// </summary>
            /// <param name="length">Length of characters you want returned, defaults to 4098 (4kb)</param>
            /// <param name="newLines">Format the returned encoded string in even lines used in PEM format</param>
            /// <returns>4kb string of random bytes encoded as base64 string (or an amount as specified by length)</returns>
            internal static async Task<string> RandomByteString(int length = 4098, bool newLines = false)
            {
                return await Base64Encode(RandomNumberGenerator.GetBytes(length), newLines);
            }

            /// <summary>
            /// Used to describe an ApiRandom object used to coinstruct cryptorandom things
            /// used within the API
            /// </summary>
            internal class ApiRandom
            {
                private readonly string _phrase;
                private readonly string _hashedPhrase;
                private readonly string _salt;
                private readonly long _iterations;
                private readonly bool _ignoreCryptoSafe;

                /// <summary>
                /// Creates an ApiRandom object that can be used to create a mnemonic phrase with accompanying hashed bytes
                /// which are cryptographically safe as entropy (assuming at least 12 words in the phrase)
                /// </summary>
                /// <param name="phrase">The phrase we will be </param>
                /// <param name="salt"></param>
                /// <param name="iterations"></param>
                internal ApiRandom(string phrase, string? salt = null, long iterations = 235017, bool ignoreCryptoSafe = false)
                {
                    if(salt.IsNullOrEmpty())
                    {
                        // Random salt between 32 to 64 bytes in length
                        salt = RandomByteString(Random.Shared.Next(32,64)).Result;
                    }

                    _ignoreCryptoSafe = ignoreCryptoSafe;
                    _iterations = iterations;
                    _phrase = phrase;
                    _salt = salt;
                    HMACSHA512 hasher = new(Encoding.Unicode.GetBytes(_phrase + _salt));
                    byte[] hashedPhraseBytes = hasher.ComputeHash(Encoding.Unicode.GetBytes(_phrase));

                    for (long i = 0; i < _iterations; i++)
                    {
                        hashedPhraseBytes = hasher.ComputeHash(hashedPhraseBytes);
                    }

                    _hashedPhrase = Convert.ToHexString(hashedPhraseBytes);
                }

                private bool CheckCryptoSafe()
                {
                    if ((WordDictionary.Count > 1999 && ((_phrase.Split('-').Length > 15) || (_phrase.Split('-').Length < 6 && _phrase.Length > 191)) && _iterations > 100000) || _ignoreCryptoSafe)
                    {
                        return true;
                    }

                    return false;
                }

                internal string Phrase { get { if (!CheckCryptoSafe()) { throw new("This ApiRandom is not Cryptosafe and we are not instructed to ignore Cryptosafety"); } return _phrase; } }
                internal string HashedPhrase { get { if (!CheckCryptoSafe()) { throw new("This ApiRandom is not Cryptosafe and we are not instructed to ignore Cryptosafety"); } return _hashedPhrase; } }
                internal byte[] HashedPhraseBytes { get { if (!CheckCryptoSafe()) { throw new("This ApiRandom is not Cryptosafe and we are not instructed to ignore Cryptosafety"); } return Convert.FromHexString(_hashedPhrase); } }
                internal string Salt { get { if (!CheckCryptoSafe()) { throw new("This ApiRandom is not Cryptosafe and we are not instructed to ignore Cryptosafety"); } return _salt; } }
                internal Guid HashedPhraseBytesAsGuid { get { if (!CheckCryptoSafe()) { throw new("This ApiRandom is not Cryptosafe and we are not instructed to ignore Cryptosafety"); } return new Guid(MD5.HashData(Convert.FromHexString(_hashedPhrase))); } }
                internal long Iterations { get => _iterations; }
            }

            /// <summary>
            /// Generates x number of 2 random word phrases from WordDictionary in format [-{0}-{1}], is cryptorandom
            /// so can be used for entropy but need a lot of 2 word phrases to create sufficiently strong entropy.
            /// </summary>
            /// <param name="numberOfPhrases">Number of 2 word phrases to generate in the single line delimited by '-'</param>
            /// <param name="salt">Optional salt to be used during sha512 hashing operation</param>
            /// <returns>List where [0] contains word phrase, [1] contains hex encoded 100,000 pass sha512</returns>
            internal static ApiRandom Random2WordPhrase(int numberOfPhrases = 1, string? salt = null, long iterations = 235017, bool ignoreCryptoSafe = false)
            {
                int i = 0;
                string phrase = string.Empty;

                if(salt.IsNullOrEmpty())
                {
                    // Random salt between 32 to 64 bytes in length
                    salt = RandomByteString(Random.Shared.Next(32, 64)).Result;
                }

                do
                {   // We can't return nothing
                    if (numberOfPhrases < 1)
                    {
                        numberOfPhrases = 1;
                    }

                    static string RandomWord()
                    {
                        return WordDictionary[RandomNumberGenerator.GetInt32(0, WordDictionary.Count)];
                    }

                    phrase += string.Format("-{0}-{1}", RandomWord(), RandomWord());

                    i++;

                } while (i < numberOfPhrases);

                return new(phrase.Remove(0, 1), salt, iterations, ignoreCryptoSafe);
            }

            /// <summary>
            /// Encrypts the provided plaintext using a random IV that is prefixed on the output encrypted string
            /// </summary>
            /// <param name="plaintext">plaintext to encrypt</param>
            /// <param name="key">encryption key (16, 24 or 32 bytes)</param>
            /// <returns>string of encrypted text (cipherText)</returns>
            /// <exception cref="ArgumentException">Will throw if AES key not a correct size</exception>
            internal static async Task<string> AesEncrypt(string plainText, byte[]? key = null, bool newLines = false)
            {
                if (key == null)
                {
                    key = await CoreEnvironment.GetDeviceIdGeneratedKey();
                }

                if (key.Length != 16 && key.Length != 24 && key.Length != 32)
                {
                    CoreEnvironment.RunErrorCount++;
                    throw new ArgumentException("AES key must be 16, 24 or 32 bytes in length");
                }

                var plainTextBuffer = Encoding.UTF8.GetBytes(plainText);

                using var aes = Aes.Create();
                aes.Key = key;

                // It is acceptable to use MD5 here as it outputs 16 bytes, it's fast, and IV is not secret
                aes.IV = MD5.HashData(await Base64Decode(await RandomByteString(512)));

                using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                using var resultStream = new MemoryStream();
                using (var aesStream = new CryptoStream(resultStream, encryptor, CryptoStreamMode.Write))
                using (var plainStream = new MemoryStream(plainTextBuffer))
                {
                    plainStream.CopyTo(aesStream);
                }

                var result = resultStream.ToArray();
                var combined = new byte[aes.IV.Length + result.Length];
                Array.ConstrainedCopy(aes.IV, 0, combined, 0, aes.IV.Length);
                Array.ConstrainedCopy(result, 0, combined, aes.IV.Length, result.Length);

                return await Base64Encode(combined, newLines);
            }

            /// <summary>
            /// Decrypts the provided cipherText and it's assumed the IV is prefixed on the cipherText
            /// </summary>
            /// <param name="cipherText">cipherText string to decrypt</param>
            /// <param name="key">decryption key</param>
            /// <returns>decrypted text (plainText)</returns>
            /// <exception cref="ArgumentException">Will throw if AES key not a correct size</exception>
            internal static async Task<string> AesDecrypt(string cipherText, byte[]? key = null)
            {
                key ??= await CoreEnvironment.GetDeviceIdGeneratedKey();

                if (key.Length != 16 && key.Length != 24 && key.Length != 32)
                {
                    CoreEnvironment.RunErrorCount++;
                    throw new ArgumentException("AES key must be 16, 24 or 32 bytes in length");
                }

                var combined = await Base64Decode(cipherText);
                var cipherTextBuffer = new byte[combined.Length];

                using var aes = Aes.Create();
                aes.Key = key;

                var iv = new byte[16];
                var ciphertext = new byte[cipherTextBuffer.Length - iv.Length];

                Array.ConstrainedCopy(combined, 0, iv, 0, iv.Length);
                Array.ConstrainedCopy(combined, iv.Length, ciphertext, 0, ciphertext.Length);

                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                using var resultStream = new MemoryStream();
                using (var aesStream = new CryptoStream(resultStream, decryptor, CryptoStreamMode.Write))
                using (var plainStream = new MemoryStream(ciphertext))
                {
                    plainStream.CopyTo(aesStream);
                }

                return Encoding.UTF8.GetString(resultStream.ToArray());
            }

            /// <summary>
            /// Generate an X509 certificate, will setup root CA and intermediaries if they are missing.
            /// </summary>
            /// <param name="publicPaths">An array of paths to save the certificate (including public key), sepcify as .cer</param>
            /// <param name="secretPaths">An array of paths to save the pcks #12 container (including encrypted private key), specify as .pfx</param>
            /// <param name="certificateType">Used to specify the type of certificate we are ordering i.e. CodeSigning</param>
            /// <param name="commonName">The CN to use on the certificate</param>
            /// <returns>Base64 encoded certificate (PEM)</returns>
            internal static async Task<string> GetCertificate(string[] publicPaths, string[] secretPaths, CoreEnvironment.CertificateType certificateType, string commonName)
            {
                // This will fail if not runing as elevated
                static async void PutCertificateInTrustStore(X509Certificate2 certificate, CoreEnvironment.CertificateType certificateType, string fileNameNoExtension)
                {
                    string logMessage = $"Root CA certificate {fileNameNoExtension}.cer placed into Windows local machine trusted root certificate store";
                    string logErrorMessage = $"Could not load CA root certificate into trusted root certificate store (Windows), this is acceptable if not running elevated (as admin): ";

                    if (CoreEnvironment.GetOperatingSystem() == CoreEnvironment.OsType.Windows)
                    {
                        X509Store certStore = new(StoreName.Root, StoreLocation.LocalMachine);

                        switch (certificateType)
                        {
                            case CoreEnvironment.CertificateType.Ca: 
                                break;

                            case CoreEnvironment.CertificateType.Intermediary:
                                certStore = new(StoreName.CertificateAuthority, StoreLocation.LocalMachine);
                                logMessage = $"Intermidiate CA certificate {fileNameNoExtension}.cer placed into Windows local machine trusted intermediary certificate store";
                                logErrorMessage = $"Could not load CA intermediate certificate into trusted intermediary certificate store (Windows), this is acceptable if not running elevated (as admin): ";
                                break;

                            default:
                                break;
                        }

                        try
                        {

                            certStore.Open(OpenFlags.OpenExistingOnly | OpenFlags.ReadWrite);
                            certStore.Add(certificate);
                            certStore.Close();

                            _ = LogsDbThreadSafeCoordinator.ThreadSafeAdd(new LogEntry()
                            {
                                Message = logMessage,
                                Severity = "Information",
                                API = "WriteCertificate"
                            });
                        }
                        catch (Exception ex)
                        {
                            CoreEnvironment.RunErrorCount++;

                            _ = LogsDbThreadSafeCoordinator.ThreadSafeAdd(new LogEntry()
                            {
                                Message = logErrorMessage + ex.Message,
                                Severity = "Error",
                                API = "WriteCertificate"
                            });
                        }

                    }
                    else if (CoreEnvironment.GetOperatingSystem() == CoreEnvironment.OsType.Linux)
                    {
                        switch (certificateType)
                        {
                            case CoreEnvironment.CertificateType.Ca:
                                logMessage = $"Root CA certificate {fileNameNoExtension}.crt placed into Linux trusted CA store";
                                logErrorMessage = $"Could not load root CA certificate into Linux trusted CA store: ";
                                break;

                            case CoreEnvironment.CertificateType.Intermediary:
                                logMessage = $"Intermediate CA certificate {fileNameNoExtension}.crt placed into Linux trusted CA store";
                                logErrorMessage = $"Could not load intermediate CA certificate into Linux trusted CA store, this is acceptable if not running elevated (as root): ";
                                break;

                            default:
                                break;
                        }

                        try
                        {
                            // Put cert in /usr/local/share/ca-certificates
                            await File.WriteAllTextAsync($"/usr/local/share/ca-certificates/{fileNameNoExtension}.crt", certificate.ExportCertificatePem());
                            // Execute update-ca-certificates to install the CA cert into trusted, this will fail if not root
                            await Cli.Wrap("/usr/sbin/update-ca-certificates").ExecuteAsync();

                            _ = LogsDbThreadSafeCoordinator.ThreadSafeAdd(new LogEntry()
                            {
                                Message = logMessage,
                                Severity = "Information",
                                API = "WriteCertificate"
                            });
                        }
                        catch (Exception ex)
                        {
                            _ = LogsDbThreadSafeCoordinator.ThreadSafeAdd(new LogEntry()
                            {
                                Message = logErrorMessage + ex.Message,
                                Severity = "Error",
                                API = "WriteCertificate"
                            });
                        }
                    }
                }

                static async Task<string> WriteCertificate(X509Certificate2 certificate, string[] publicPaths, string[] secretPaths, CoreEnvironment.CertificateType certificateType, ECDsa keyPair, bool import=false)
                {
                    if (!certificate.HasPrivateKey)
                    {
                        certificate = certificate.CopyWithPrivateKey(keyPair);
                    }

                    if(import)
                    {
                        string filenameNoExtension;

                        PutCertificateInTrustStore(certificate, certificateType, publicPaths[0].Split(Path.DirectorySeparatorChar)[^1].Replace(".cer","").Replace(".crt","").Replace(".pem",""));
                    }

                    foreach (string pubPath in publicPaths)
                    {
                        // Create Base 64 encoded CER (public key only)
                        string exportPem = certificate.ExportCertificatePem();
                        await File.WriteAllTextAsync(pubPath, exportPem);

                        switch (certificateType)
                        {
                            case CoreEnvironment.CertificateType.Ca:
                                CoreEnvironment.CaRootCertPem = exportPem;
                                break;

                            case CoreEnvironment.CertificateType.Intermediary:
                                if(pubPath.Contains(CoreEnvironment.CurrentCaIntermediateCertName))
                                {
                                    CoreEnvironment.CurrentCaIntermediateCertPem = exportPem;
                                }
                                break;

                            default:
                                // Bundle our signing Intermediary CA cert into this certificate as well
                                await File.AppendAllTextAsync(pubPath, $"\n{CoreEnvironment.CurrentCaIntermediateCertPem}");
                                break;
                        }
                    }

                    // Create PFX (PKCS #12) with private key which is encrypted by a password
                    foreach (string secPath in secretPaths)
                    {
                        await File.WriteAllBytesAsync(secPath, certificate.Export(X509ContentType.Pfx, await Base64Encode(await CoreEnvironment.GetDeviceIdGeneratedKey((int)certificateType))));
                    }

                    return await Base64Encode(certificate.Export(X509ContentType.Cert), false);
                }

                static async Task<string?> CreateCertificate(string[] publicPaths, string[] secretPaths, CoreEnvironment.CertificateType certificateType, string? commonName)
                {
                    ECDsa ecdsaKeyPair = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                    HashAlgorithmName hashAlgoName = HashAlgorithmName.SHA256;
                    X509KeyUsageFlags certificateUseFlags = X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.DigitalSignature;
                    X509BasicConstraintsExtension basicConstraints = new(false, true, 0, true);
                    string[] pubPaths = publicPaths;
                    string[] secPaths = secretPaths;
                    X509Certificate2? issuerCert = null;
                    X509Extension? certificateRevocvationList = null;
                    X509Certificate2? returnCertificate = null;
                    certificateRevocvationList = CertificateRevocationListBuilder.BuildCrlDistributionPointExtension(new[] { $"{CoreEnvironment.FrontEndUri.ToLower()}/pki/crl/rocketrmm.crl" });
                    bool importCert = false;

                    switch (certificateType)
                    {
                        case CoreEnvironment.CertificateType.Ca:
                            commonName = $"CN=\"RocketRMM - {await CoreEnvironment.GetDeviceTag()} - Root CA\",O=\"RocketRMM\"";
                            ecdsaKeyPair = ECDsa.Create(ECCurve.NamedCurves.nistP521);
                            hashAlgoName = HashAlgorithmName.SHA512;
                            certificateUseFlags = X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.DigitalSignature;
                            basicConstraints = new X509BasicConstraintsExtension(true, true, 1, true);
                            pubPaths = [$"{CoreEnvironment.CaDir}{Path.DirectorySeparatorChar}{CoreEnvironment.CaRootCertName}.cer", $"{CoreEnvironment.WebRootPath}{Path.DirectorySeparatorChar}pki{Path.DirectorySeparatorChar}ca{Path.DirectorySeparatorChar}{CoreEnvironment.CaRootCertName}.cer"];
                            secPaths = [$"{CoreEnvironment.CaDir}{Path.DirectorySeparatorChar}{CoreEnvironment.CaRootCertName}.pfx"];
                            importCert = true;
                            break;

                        case CoreEnvironment.CertificateType.Intermediary:
                            ecdsaKeyPair = ECDsa.Create(ECCurve.NamedCurves.nistP521);
                            hashAlgoName = HashAlgorithmName.SHA512;
                            certificateUseFlags = X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.DigitalSignature;
                            basicConstraints = new X509BasicConstraintsExtension(true, true, 0, true);
                            string password = await Base64Encode(await CoreEnvironment.GetDeviceIdGeneratedKey((int)CoreEnvironment.CertificateType.Ca));
                            issuerCert = new X509Certificate2(fileName: $"{CoreEnvironment.CaDir}{Path.DirectorySeparatorChar}{CoreEnvironment.CaRootCertName}.pfx", password: await Base64Encode(await CoreEnvironment.GetDeviceIdGeneratedKey((int)CoreEnvironment.CertificateType.Ca)));
                            importCert = true;
                            break;

                        case CoreEnvironment.CertificateType.Authentication:
                            certificateUseFlags = X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation | X509KeyUsageFlags.KeyAgreement | X509KeyUsageFlags.DataEncipherment;
                            issuerCert = new X509Certificate2(fileName: $"{CoreEnvironment.CaIntermediateDir}{Path.DirectorySeparatorChar}{CoreEnvironment.CurrentCaIntermediateCertName}.pfx", password: await Base64Encode(await CoreEnvironment.GetDeviceIdGeneratedKey((int)CoreEnvironment.CertificateType.Intermediary)));
                            break;

                        default:
                            if(commonName.IsNullOrEmpty())
                            {
                                commonName = $"CN=\"RocketRMM - {await CoreEnvironment.GetDeviceTag()}";
                            }
                            break;
                    }

                    X500DistinguishedName distinguishedName = new(commonName);                   
                    var certificateRequest = new CertificateRequest(distinguishedName, ecdsaKeyPair, hashAlgoName);
                    certificateRequest.CertificateExtensions.Add(new X509KeyUsageExtension(certificateUseFlags, true));
                    certificateRequest.CertificateExtensions.Add(basicConstraints);
                    certificateRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(certificateRequest.PublicKey, false));
                    certificateRequest.CertificateExtensions.Add(certificateRevocvationList);

                    if(issuerCert == null)
                    {
                        returnCertificate = certificateRequest.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(100));
                    }
                    else
                    {
                        returnCertificate = certificateRequest.Create(issuerCert, DateTimeOffset.Now, issuerCert.NotAfter, new X509SubjectKeyIdentifierExtension(certificateRequest.PublicKey, false).SubjectKeyIdentifierBytes.ToArray());
                    }

                    if (returnCertificate != null)
                    {
                        return await WriteCertificate(returnCertificate, pubPaths, secPaths, certificateType, ecdsaKeyPair, importCert);
                    }

                    return null;
                }

                // If our Root CA does not exist, create it
                if (!File.Exists($"{CoreEnvironment.CaDir}{Path.DirectorySeparatorChar}{CoreEnvironment.CaRootCertName}.pfx"))
                {
                    // Make sure any intermediaries do not exist if we don't have a root CA
                    foreach(string filePath in Directory.EnumerateFiles(CoreEnvironment.CaIntermediateDir))
                    {
                        File.Delete(filePath);
                    }
                    _= await CreateCertificate([""], [""], CoreEnvironment.CertificateType.Ca,null);
                }

                // If any of our Intermediate CAs do not exist, create them
                foreach (string filename in CoreEnvironment.CaIntermediateCertNames)
                {
                    if (!File.Exists($"{CoreEnvironment.CaIntermediateDir}{Path.DirectorySeparatorChar}{filename}.pfx"))
                    {
                        _ = await CreateCertificate([$"{CoreEnvironment.CaIntermediateDir}{Path.DirectorySeparatorChar}{filename}.cer", $"{CoreEnvironment.WebRootPath}{Path.DirectorySeparatorChar}pki{Path.DirectorySeparatorChar}ca{Path.DirectorySeparatorChar}{filename}.cer"], [$"{CoreEnvironment.CaIntermediateDir}{Path.DirectorySeparatorChar}{filename}.pfx"], CoreEnvironment.CertificateType.Intermediary, $"CN=\"RocketRMM - {await CoreEnvironment.GetDeviceTag()} - Intermediate CA{filename[^1..]}\",O=\"RocketRMM\"");
                    }
                }

                return await CreateCertificate(publicPaths, secretPaths, certificateType, commonName);
            }
        }

    }
}
