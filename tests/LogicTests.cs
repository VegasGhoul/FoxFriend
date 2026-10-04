using System;
using Lisichka;
class LogicTests {
    static int n;
    static void Expect(bool value,string label){n++;if(!value)throw new Exception("FAIL: "+label);}
    static void Command(string text,Pose? expected){Expect(Intent.Command(text)==expected,text);}
    static void Main(){
        Command("Лисичка, сядь пожалуйста",Pose.Sit);Command("Присядь рядом",Pose.Sit);Command("Можешь немного посидеть?",Pose.Sit);
        Command("Полежи",Pose.Lie);Command("Ложись рядом со мной",Pose.Lie);Command("Приляг на минутку",Pose.Lie);Command("Хочу, чтобы ты полежала",Pose.Lie);
        Command("Ложись спать",Pose.Sleep);Command("Пора спать",Pose.Sleep);Command("Отправляйся в кроватку",Pose.Sleep);Command("Сладко спи",Pose.Sleep);
        Command("Проснись",Pose.Sit);Command("Не спи",Pose.Sit);Command("Не ложись",null);Command("Не надо ложиться спать",null);Command("Не садись, а полежи",Pose.Lie);
        Command("Я хочу спать",null);Command("Мне пора спать",null);Command("Как твои дела?",null);
        Expect(Intent.Negative("Мне очень плохо"),"bad mood");Expect(Intent.Negative("Все хорошо, но мне грустно"),"mixed mood");Expect(Intent.Negative("Не очень хорошо"),"negative good");Expect(!Intent.Positive("Не очень хорошо"),"not positive");Expect(Intent.Positive("Все замечательно"),"positive");Expect(!Intent.Negative("Мне не грустно"),"negated sad");
        Expect(Companion.Greeting(new DateTime(2026,10,4,8,0,0)).StartsWith("Доброе утро"),"morning");Expect(Companion.Greeting(new DateTime(2026,10,4,19,0,0)).StartsWith("Добрый вечер"),"evening");
        Expect(!Companion.Night(new DateTime(2026,10,3,23,59,59)),"before midnight");Expect(Companion.Night(new DateTime(2026,10,4,0,0,0)),"midnight");Expect(!Companion.Night(new DateTime(2026,10,4,7,0,0)),"morning wake");
        var c=new Companion();Expect(c.Respond("Обними меня").Affection,"affection");Expect(c.Respond("Ложись спать").Action==Pose.Sleep,"response action");
        var mind=new FoxMind();
        foreach(var sample in new[]{
            new[]{"Я очень сильно тебя люблю, лисичка!","love"},new[]{"обажаю тебя","love"},new[]{"ты любишь меня?","love_question"},
            new[]{"ты такая хорошенькая","compliment"},new[]{"Приветик, лисичка","greeting"},new[]{"Благодарю тебя за поддержку","thanks"},
            new[]{"пошути пожалуйста","joke"},new[]{"мне сейчас очень скучно","bored"},new[]{"скажи пожалуйста который час","time"},
            new[]{"очень одиноко сегодня","lonely"},new[]{"лисичка мне нужна поддержка","support"}}){var match=mind.Classify(sample[0]);Expect(match!=null&&match.Label==sample[1],"held-out model: "+sample[0]+" predicted "+(match==null?"unknown":match.Label));}
        Expect(c.Respond("Я тебя люблю").Affection,"love animation");
        Expect(c.Respond("Я тебя люблю").Text.Contains("люб"),"love reply");
        Expect(!c.Respond("Я тебя не люблю").Affection,"negated love");
        Expect(!c.Respond("Не обнимай меня").Affection,"negated hug");
        Expect(!c.Respond("Я люблю картошку").Affection,"love about food");
        Expect(!c.Respond("Я люблю свою маму").Affection,"love about somebody else");
        Expect(!c.Respond("Ты любишь музыку?").Affection,"question about other object");
        Expect(!c.Respond("Что ты любишь?").Affection,"preferences are not a confession");
        string returned=c.Respond("Я вернулся").Text;Expect(returned.Contains("возвращением")||returned.Contains("заглянул"),"return greeting");
        Expect(c.Respond("🧡").Affection,"heart response");
        Expect(c.Respond("Меня зовут Кирилл").Text.Contains("Кирилл"),"learn name in session");
        Expect(c.Respond("Как меня зовут").Text.Contains("Кирилл"),"recall name");
        Expect(!new Companion().Respond("Как меня зовут").Text.Contains("Кирилл"),"no cross-session name memory");
        string first=c.Respond("Расскажи шутку").Text;Expect(c.Respond("Расскажи шутку").Text!=first,"avoid immediate repeated answer");
        var conversation=new FoxMind();conversation.Respond("Мне скучно");Expect(conversation.Respond("Да").Text.Contains("три вещи"),"contextual yes to game");
        conversation.Respond("Мне грустно");Expect(conversation.Respond("Не сейчас").Text.Contains("необязательно"),"contextual no to sharing");
        Expect(mind.Classify("квантовая хромодинамика") == null,"unknown technical topic");
        Expect(mind.Classify("абракадабразю") == null,"unknown noise");
        Console.WriteLine("FoxMind: "+mind.TopicCount+" topics, "+mind.ExampleCount+" training phrases, "+mind.FeatureCount+" learned features");
        Expect(InputPolicy.EnterSends(SendMode.Enter,false),"Enter mode");Expect(!InputPolicy.ArrowSends(SendMode.Enter),"Enter excludes arrow");Expect(!InputPolicy.EnterSends(SendMode.Arrow,false),"Arrow excludes Enter");Expect(InputPolicy.ArrowSends(SendMode.Arrow),"Arrow mode");Expect(InputPolicy.EnterSends(SendMode.Both,false)&&InputPolicy.ArrowSends(SendMode.Both),"Both mode");foreach(SendMode mode in Enum.GetValues(typeof(SendMode)))Expect(!InputPolicy.EnterSends(mode,true),"Shift Enter newline "+mode);
        Expect(Names.Read("Кирилл",true)=="Кирилл","first name response");Expect(Names.Read("Привет",true)=="","greeting not a name");Expect(Names.Read("Кирилл",false)=="","unsolicited word not a name");Expect(Names.Read("Меня зовут Анна",false)=="Анна","explicit name change");Expect(Names.Form("Кирилл","",true)=="Кирюша","tender name");Expect(Names.Form("Кирилл","Кир",true)=="Кир","custom nickname");Expect(Names.Form("Кирилл","Кир",false)=="Кирилл","tender names disabled");Expect(Names.Form("Али","",true)=="Али","do not invent unfamiliar name forms");Expect(Names.Clean("<script>")=="","reject invalid name");
        Expect(InputPolicy.Scale(double.NaN)==1&&InputPolicy.Scale(100)==1.4&&InputPolicy.Scale(0)==.6,"scale bounds");
        var drag=new DragSession();double left,top;Expect(!drag.Update(5,5,true,out left,out top),"motion without press");drag.Start(100,100,500,400);Expect(!drag.Update(101,101,true,out left,out top)&&!drag.Moved,"click jitter");Expect(drag.Update(-100,220,true,out left,out top)&&left==300&&top==520,"free drag across screen");Expect(!drag.Update(-100,220,false,out left,out top)&&!drag.Active,"button released during move");drag.Finish();Expect(!drag.Update(10,10,true,out left,out top),"late event after capture loss");
        for(int i=0;i<500;i++){drag.Start(2,3,-100,60);drag.Update(i,-i,i%2==0,out left,out top);drag.Finish();}Expect(!drag.Active,"500 drag start stop races");
        Expect(c.Respond("Я спать! Спокойной ночи").Text.Contains("Спокойной ночи"),"compound goodnight reply");
        Expect(c.Respond("Спокойной ночи! Ложись спать!").Action==Pose.Sleep,"compound goodnight action");
        Expect(c.Respond("Полежи, пока я читаю книгу").Action==Pose.Lie,"topic does not swallow command");
        Expect(c.Respond("Сколько будет 23 + 19?").Text.Contains("42"),"arithmetic");
        Expect(c.Respond("5 / 0").Text.Contains("ноль"),"division by zero");
        var analyzer=new ConversationAnalyzer();string story="На работе проект и дедлайн, коллеги помогают. Потом я читал книгу и обсуждал роман. "+new string(' ',270);
        Expect(analyzer.Respond(story).Text.Contains("Выделила темы"),"long topic analysis");Expect(analyzer.Respond("второе").Text.Contains("книги"),"topic selection context");Expect(analyzer.Respond("выдели главное").Text.Contains("работа"),"recall previous story");
        Expect(analyzer.Respond(new string('я',30000)).Text.Contains("нет готовых знаний"),"long unknown honest fallback");
        Expect(ConversationAnalyzer.HeartCount("Я победил",false)==12,"achievement hearts");Expect(ConversationAnalyzer.HeartCount("Победил, но мне грустно",false)==2,"negative mood takes priority");Expect(ConversationAnalyzer.HeartCount("Очень тебя люблю",true)==10,"affection hearts");Expect(ConversationAnalyzer.HeartCount("Привет",false)==1,"neutral hearts");
        foreach(var phrase in new[]{"Я пошел спать","Я пошла спать","пойду уже спать","Я иду спать"}){var r=new Companion().Respond(phrase);Expect(r.Text.Contains("ноч")||r.Text.Contains("снов"),"bedtime: "+phrase);Expect(!r.Action.HasValue,"user bedtime is not pet command: "+phrase);}
        foreach(var phrase in new[]{"Повеляй хвостиком","Повиляй хвостиком","Помаши хвостом","Можешь пошевелить хвостиком?"})Expect(new Companion().Respond(phrase).TailAction==1,"tail request: "+phrase);
        Expect(new Companion().Respond("Не виляй хвостиком").TailAction==-1,"stop tail");
        Expect(new Companion().Respond("Можешь поддержать").Text.Contains("поддержу"),"support screenshot");
        Expect(new Companion().Respond("Писимистично").Text.Contains("мрачным"),"pessimistic typo screenshot");
        Expect(new Companion().Respond("спокойного дня").Text.Contains("И тебе"),"day wish screenshot");
        Expect(new Companion().Respond("умница").Affection,"praise screenshot");
        var workChat=new Companion();workChat.Respond("У меня сложности на работе");Expect(workChat.Respond("разговор").Text.Contains("С кем"),"work conversation screenshot");
        var supportChat=new Companion();supportChat.Respond("Можешь поддержать");Expect(supportChat.Respond("выслушай").Text.Contains("слушаю"),"support remembers choice");
        foreach(var pair in new[]{new[]{"Я злюсь","злост"},new[]{"Мне обидно","Больно"},new[]{"Я виноват","ошибку"},new[]{"Мне стыдно","стыдно"},new[]{"Я ревную","Ревность"},new[]{"Я завидую","Зависть"},new[]{"Ничего не хочется","хочется"},new[]{"Я растерян","название"},new[]{"Я не уверен в себе","строго"},new[]{"Я интроверт","тишине"},new[]{"Я экстраверт","общение"},new[]{"Я перфекционист","идеальный"},new[]{"Я оптимист","надежда"},new[]{"Я реалист","сложности"},new[]{"Я скептик","обещаний"}})Expect(new Companion().Respond(pair[0]).Text.Contains(pair[1]),"emotion/view: "+pair[0]);
        Expect(new Companion().Respond("Я не злюсь").Text.Contains("не описывает"),"negated emotion");
        Expect(new Companion().Respond("Я не пессимист, я реалист").Text.Contains("сложности"),"contrasting outlook");
        Expect(new Companion().Respond("Мне и радостно и грустно").Text.Contains("одновременно"),"mixed feelings");
        string thanks=new Companion().Respond("Спасибо за поддержку").Text.ToLowerInvariant();Expect(thanks.Contains("пожалуйста")||thanks.Contains("спасибо"),"thanks not support request");
        var changedTopic=new Companion();changedTopic.Respond("Поддержи меня");changedTopic.Respond("Есть вопрос по работе");Expect(changedTopic.Respond("разговор").Text.Contains("С кем"),"new topic replaces support context");
        Expect(new Companion().Respond("Я спать не хочу").Text.Contains("пока не"),"bedtime negation");
        Expect(new Companion().Respond("Можешь повелять хвостиком?").TailAction==1,"tail infinitive typo");
        foreach(string request in new[]{"Предложи тему разговора","Можешь предложить тему?","О чём поговорим?","Про что поболтать?"})Expect(new Companion().Respond(request).Text.Contains("1. Музыка"),"topic suggestion: "+request);
        var talk=new Companion();string menu=talk.Respond("Предложи тему разговора").Text;Expect(menu.Contains("2. Фильмы")&&menu.Contains("3. Игры"),"three suggested choices");
        Expect(talk.Respond("вторую").Text.Contains("Какой фильм"),"ordinal selection");Expect(talk.Respond("Интерстеллар").Text.Contains("герой"),"film answer continues");Expect(talk.Respond("атмосфера").Text.Contains("момент"),"short answer continues selected topic");Expect(talk.Respond("а ты?").Text.Contains("сказку"),"reciprocal topic question");Expect(talk.Respond("напомни тему").Text.Contains("атмосфера"),"topic memory");
        Expect(talk.Respond("другие темы").Text.Contains("Мечты"),"different suggestions");Expect(talk.Respond("выбери сама").Text.Contains("давно хочется"),"fox chooses offered topic");Expect(talk.Respond("не знаю").Text.Contains("Необязательно"),"gentle unknown followup");
        var music=new Companion();music.Respond("Давай поговорим о музыке");Expect(music.Respond("Мне нравится рок").Text.Contains("В роке"),"specific music continuation");Expect(music.Respond("звучание").Text.Contains("песня"),"second music followup");Expect(music.Respond("Повиляй хвостиком").TailAction==1,"action during conversation");Expect(music.Respond("Мне грустно").Text.Contains("грустно"),"support during conversation");Expect(music.Respond("Я тебя люблю").Affection,"affection during conversation");Expect(music.Respond("Ложись спать").Action==Pose.Sleep,"pose during conversation");
        var guide=new TopicConversation();guide.Control("Предложи тему");Expect(guide.Control("Фильмы и сериалы")!=null,"full title selection");Expect(guide.Control("Не хочу обсуждать фильмы")!=null&&!guide.Active,"end conversation");Expect(guide.Continue("что-то") ==null,"no followups after stopping");Expect(new Companion().Respond("2").Text.IndexOf("Какой фильм",StringComparison.Ordinal)<0,"no invented menu in fresh session");
        var longTalk=new TopicConversation();longTalk.Control("Давай про книги");for(int i=0;i<8;i++)Expect(longTalk.Continue("Мне нравится этот мир").Text.Length<500,"bounded multi-turn conversation "+i);
        var books=new Companion();books.Respond("Давай про книги");string titleReply=books.Respond("Четвертое крыло").Text;Expect(titleReply.Contains("Четвертое крыло")&&titleReply.Contains("читаешь"),"capture book title");
        string sameTopic=books.Respond("книги").Text;Expect(sameTopic.Contains("Четвертое крыло")&&!sameTopic.Contains("Какую книгу"),"same topic preserves title");
        string repeatedTitle=books.Respond("Книгу Четвертое крыло").Text;Expect(repeatedTitle.Contains("название помню")&&!repeatedTitle.Contains("Ты упомянул"),"rephrased title does not restart");
        Expect(books.Respond("Ещё читаю").Text.Contains("до чего ты дошёл"),"reading status");Expect(books.Respond("мир книги").Text.Contains("деталь"),"world branch");Expect(books.Respond("Драконы").Text.Contains("отношения с людьми"),"react to story detail");
        Expect(books.Respond("помнишь название").Text.Contains("Четвертое крыло"),"title survives multiple replies");Expect(books.Respond("Ты знаешь эту книгу?").Text.Contains("нет надёжной справки"),"do not fake book knowledge");
        Expect(!books.Respond("Драконы").Text.Contains("Что тебе в них интересно"),"do not repeat same branch question");
        var noLoop=new TopicConversation();noLoop.Control("Давай про музыку");var unique=new System.Collections.Generic.HashSet<string>();for(int i=0;i<5;i++){string answer=noLoop.Continue("дальше").Text;Expect(unique.Add(answer),"no question cycle "+i);}Expect(!noLoop.Continue("дальше").Text.Contains("цепляет"),"no restart after question exhaustion");
        books.Respond("Давай про фильмы");Expect(!books.Respond("напомни тему").Text.Contains("Четвертое крыло"),"new topic clears work title");
        foreach(string phrase in new[]{"закончим тему","Давай закончим эту тему","Лисичка, закроем тему, пожалуйста","Можно закончить разговор?","Хочу завершить обсуждение","Не хочу больше об этом говорить","Я больше не хочу обсуждать книги","Хватит про книги","Давай не будем об этом","Мне надоела эта тема","Прекрати спрашивать про книги","Ладно, отложим пока разговор"}){
            var flow=new TopicConversation();flow.Control("Давай про книги");flow.Continue("Четвертое крыло");var stopped=flow.Control(phrase);Expect(stopped!=null&&!flow.Active&&flow.Continue("дальше")==null,"release topic: "+phrase);
        }
        foreach(string phrase in new[]{"Сменим тему","Давай поменяем тему","Хочу сменить тему","Можешь сменить тему?","Можно поговорить о другом?","Давай о чём-нибудь другом","Хочу поговорить на другую тему","Теперь лучше про другое","Переключимся на другую тему","Предложи другую тему разговора"}){
            var flow=new TopicConversation();flow.Control("Давай про книги");var switched=flow.Control(phrase);Expect(switched!=null&&switched.Text.Contains("1.")&&!flow.Active,"switch topic: "+phrase);
        }
        foreach(string phrase in new[]{"Давай поговорим","Я хочу просто поговорить","Поговори со мной","Можно поболтать?","Хочу пообщаться","Лисичка, давай просто поболтаем","Хочется поговорить с тобой"}){
            var flow=new TopicConversation();flow.Control("Давай про книги");var free=flow.Control(phrase);Expect(free!=null&&free.Text.Contains("на уме")&&!flow.Active,"free talk: "+phrase);
        }
        foreach(string phrase in new[]{"А давай лучше о музыке","Теперь хочу поговорить про музыку","Закончим тему и давай поговорим о музыке"}){var flow=new Companion();flow.Respond("Давай про книги");Expect(flow.Respond(phrase).Text.Contains("Какая музыка"),"named switch: "+phrase);}
        foreach(string phrase in new[]{"Не меняй тему","Я не хочу заканчивать тему","Не надо менять тему"}){var flow=new TopicConversation();flow.Control("Давай про книги");Expect(flow.Control(phrase)!=null&&flow.Active,"negated stop: "+phrase);}
        foreach(string phrase in new[]{"Я закончил книгу","Герой сказал: закончим тему","В книге поменяли тему разговора"}){var flow=new TopicConversation();flow.Control("Давай про книги");Expect(flow.Control(phrase)==null&&flow.Active,"not a control request: "+phrase);}
        var phoneChat=new Companion();phoneChat.Respond("Давай про книги");phoneChat.Respond("Четвертое крыло");Expect(phoneChat.Respond("У меня сломался телефон").Text.Contains("техник"),"new subject bypasses book guide");Expect(!phoneChat.Respond("дальше").Text.Contains("мир книги"),"old topic does not return");
        var mathChat=new Companion();mathChat.Respond("Давай про книги");Expect(mathChat.Respond("Сколько будет 23 + 19?").Text.Contains("42"),"math during topic");
        var fresh=new Companion();fresh.Respond("Давай про книги");fresh.Respond("закончим тему");Expect(!fresh.Respond("Мне нравится рок").Text.Contains("мир книги"),"after stop routes fresh message");
        var workStop=new Companion();workStop.Respond("Есть вопрос по работе");workStop.Respond("Закончим тему");Expect(!workStop.Respond("разговор").Text.Contains("С кем"),"stop clears analyzer context");
        var unrelated=new TopicConversation();unrelated.Control("Давай про книги");unrelated.Continue("Четвертое крыло");Expect(unrelated.Continue("Что ты умеешь?")==null&&!unrelated.Active,"unknown question not book continuation");
        var unknownSwitch=new TopicConversation();unknownSwitch.Control("Давай про книги");Expect(unknownSwitch.Control("Давай поговорим о садоводстве")!=null&&!unknownSwitch.Active,"unknown destination releases topic");
        foreach(string phrase in new[]{"Закончи эту тему","С этой темой закончили","Не хочу об этом","Закончим с этой темой"}){var flow=new TopicConversation();flow.Control("Давай про книги");Expect(flow.Control(phrase)!=null&&!flow.Active,"more stop forms: "+phrase);}
        foreach(string phrase in new[]{"Хочу обсудить другую тему","Давай начнем новую тему","Что-нибудь другое"}){var flow=new TopicConversation();flow.Control("Давай про книги");Expect(flow.Control(phrase).Text.Contains("1.")&&!flow.Active,"more switch forms: "+phrase);}
        foreach(string phrase in new[]{"Хочу с тобой поговорить","Давай поговорим на любую тему","Давай просто поговорим ни о чем"}){var flow=new TopicConversation();flow.Control("Давай про книги");Expect(flow.Control(phrase).Text.Contains("на уме")&&!flow.Active,"more free talk forms: "+phrase);}
        var recall=new Companion();recall.Respond("Давай про книги");recall.Respond("Четвертое крыло");Expect(recall.Respond("О чем мы говорили?").Text.Contains("Четвертое крыло"),"memory question not topic change");Expect(recall.Respond("О чем она?").Text.Contains("нет надёжной справки"),"anaphoric book question");
        Expect(recall.Respond("Давай обсудим музыку").Text.Contains("Какая музыка"),"discuss named subject");
        Console.WriteLine("PASS: "+n+" behavior checks");
    }
}
