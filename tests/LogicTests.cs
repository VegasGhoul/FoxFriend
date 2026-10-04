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
        Console.WriteLine("PASS: "+n+" behavior checks");
    }
}
