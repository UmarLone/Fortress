using System.Collections.Generic;

namespace Fortress.Mobile.Core.Utilities
{
    /// <summary>
    /// Provides a curated word list for passphrase generation.
    /// ~500 common English words → 500⁴ ≈ 62.5 billion 4-word combinations.
    /// </summary>
    public sealed class EEFLongWordList
    {
        private static volatile EEFLongWordList _uniqueInstance;
        private static readonly object _syncObj = new();

        private EEFLongWordList() { }

        public static EEFLongWordList Instance
        {
            get
            {
                if (_uniqueInstance == null)
                {
                    lock (_syncObj)
                    {
                        _uniqueInstance ??= new EEFLongWordList();
                    }
                }
                return _uniqueInstance;
            }
        }

        public List<string> List { get; } = new(Words);

        private static readonly string[] Words =
        [
     "abacus","acid","acorn","acre","actor","adrift","aerial","afar","agenda","agile",
  "aglow","alarm","album","alder","alert","alibi","align","alive","alley","alpha",
          "amber","amid","amuse","anchor","angel","angle","ankle","anvil","apart","apex",
 "apple","apron","arbor","arena","armor","arrow","atlas","atom","attic","audio",
       "avid","awake","axis","azure","badge","bagel","baker","balm","bamboo","banjo",
     "barge","baron","basin","batch","beacon","beast","begin","bench","berry","birch",
            "blade","blank","blaze","blend","blimp","block","bloom","bluff","board","bonus",
            "booth","bound","brace","brain","brave","bread","briar","brick","brief","brisk",
            "broad","brook","brush","budge","bulk","bunch","burst","cabin","cable","camel",
        "candy","canon","cargo","carry","carve","cedar","chain","chalk","charm","chase",
            "cheap","chess","chief","chord","cinch","civic","claim","clamp","clash","clasp",
            "clean","clear","clerk","cliff","climb","cling","clock","close","cloth","cloud",
     "clown","coach","coast","cobra","cocoa","comet","coral","couch","cover","craft",
  "crane","crash","crawl","crazy","creek","crest","crisp","cross","crowd","crown",
            "crush","cubic","curve","cycle","dagger","daily","dairy","dance","darts","dawn",
         "decay","decoy","delta","demon","depot","depth","derby","desk","dew","diary",
 "digit","dime","dingo","diver","dock","dodge","donor","dove","draft","drain",
      "drape","drawn","dream","dress","drift","drill","drink","drive","drum","dusk",
            "eagle","earth","easel","ebony","echo","edge","eight","elbow","elder","elite",
       "ember","emu","enjoy","entry","equal","error","essay","ethic","event","exact",
            "exile","extra","fable","facet","fairy","faith","false","fancy","fang","fault",
  "feast","fence","ferry","fetch","fever","fiber","field","fifth","fifty","finch",
   "flame","flask","fleet","flint","flock","flood","floor","flora","floss","fluid",
          "flute","focal","foggy","folly","force","forge","forum","found","frame","fresh",
  "frost","fruit","gauge","gavel","gecko","genre","ghost","giant","giddy","glade",
"glare","glass","gleam","glide","globe","gloom","glove","glyph","goat","going",
        "gorge","gourd","grace","grain","grand","grant","grape","grasp","grass","grave",
  "greed","green","greet","grief","grill","grind","gripe","groin","groom","group",
            "grove","growl","grown","guard","guest","guide","guild","guilt","guise","gulch",
    "gummy","guru","habit","half","haven","hawk","hazel","heart","hedge","helix",
       "heron","hiker","hinge","hippo","hitch","hive","hobby","hoist","holly","honey",
   "honor","horns","horse","hotel","hover","humid","humor","husky","hydro","hyena",
            "igloo","image","ivory","jewel","joker","joust","judge","juice","jumbo","kayak",
   "kebab","kiosk","kite","knack","kneel","knelt","knife","knit","knock","knoll",
            "label","lager","lance","latch","lemon","level","light","lilac","linen","llama",
   "lobby","lodge","lofty","logic","lotus","lunar","lunch","magma","major","mango",
      "manor","maple","march","marsh","mason","match","mayor","melon","mercy","merit",
   "mesa","metal","midst","mimic","minor","mirth","model","modem","money","moose",
            "moral","mossy","motor","motto","mound","mount","mouse","mouth","movie","mulch",
            "mural","music","myth","nacho","naive","navel","nerve","noble","noise","north",
     "noted","novel","nudge","nurse","nylon","oasis","ocean","olive","omega","onion",
        "onset","opera","orbit","organ","other","otter","outer","ovary","oxide","ozone",
            "paced","paddy","paint","panda","panel","panic","paper","parse","party","pasta",
         "patch","patio","pause","peach","pearl","pedal","penny","perch","petal","phase",
"phone","photo","piano","pilot","pinch","pixel","pizza","place","plain","plane",
            "plank","plant","plaza","plead","pluck","plumb","plume","plump","plunge","point",
   "poise","polar","poppy","porch","poser","pouch","pound","power","press","price",
       "pride","prime","print","prior","prism","prize","probe","prong","proof","prose",
   "proud","prune","pulse","punch","pupil","purse","quail","qualm","quart","queen",
 "query","quest","quick","quiet","quirk","quota","quote","radar","radio","raise",
  "rally","ramp","ranch","range","rapid","raven","reach","realm","rebel","reign",
            "relay","relic","renew","repay","resin","ridge","rigid","ripen","risen","risky",
    "rival","river","robot","rocky","rogue","roost","round","route","royal","rugby",
          "ruler","rural","sabre","saint","salad","salon","salsa","sandy","satin","sauce",
            "sauna","scale","scarf","scene","scent","scope","score","scout","shade","shaft",
            "shame","shape","shark","sharp","shawl","sheep","shelf","shell","shift","shine",
       "shirt","shock","shore","shout","shown","shrub","siege","sight","sigma","silky",
        "silly","since","siren","sixth","sixty","skate","skill","skull","slate","sleep",
            "slice","slide","slope","smart","smell","smile","smoke","snack","snake","solar",
       "solid","solve","sonic","south","space","spare","spark","spawn","speak","spear",
          "spell","spend","spice","spike","spine","spoke","spoon","spray","squad","stack",
         "staff","stage","stain","stake","stale","stalk","stamp","stand","stark","start",
            "state","stays","steam","steel","steep","steer","stern","stick","stiff","sting",
         "stock","stomp","stone","stood","stool","store","storm","story","stout","stove",
          "strip","strut","stuck","study","stuff","stump","style","sugar","suite","sunny",
         "super","surge","swamp","swarm","swear","sweep","swift","swirl","sword","syrup",
         "table","talon","tango","tangy","tapir","tease","tempo","tense","theta","thick",
            "thorn","three","throw","thumb","tiger","timer","toast","token","tonic","topaz",
 "torch","totem","touch","towel","tower","toxic","trace","track","trade","trail",
      "train","trait","tramp","trash","tread","treat","trend","trial","tribe","trick",
       "troop","trout","truck","truly","trump","trunk","trust","truth","tulip","tunic",
         "turbo","tutor","tweed","twice","twirl","twist","ultra","umbra","uncle","under",
       "union","unite","unity","until","upper","urban","usage","usher","usual","utter",
            "vague","valid","valve","vapor","vault","verse","video","vigor","vinyl","viola",
 "viper","vivid","vocal","voice","voter","vowel","wafer","wagon","waltz","watch",
      "water","wheat","wheel","whole","width","widen","witch","woman","world","wound",
       "wrist","yacht","yearn","yield","young","youth","zebra","zinc","zombie","zone",
        ];
    }
}
