using System;
using BepInEx;
using UnityEngine;
using HarmonyLib;
using System.Collections;

namespace QTools
{
    [BepInPlugin("me.sky.plugin.Dyson.QTools", "QTools", "3.2")]
    public class QTools : BaseUnityPlugin
    {
        Rect windowRect = new Rect(0, 0, 0, 0);//窗口
        bool keyLock = false;//键盘锁定flag
        bool mouseLock = false;//鼠标锁定flag
        Vector3 lastMousePosition;//上一个鼠标位置
        Hashtable translateMap = new Hashtable();//翻译用的HashTable
        int max = 40;//每行最大格子数
        float line = 2;//格子间距
        float leftAndRight = 10;//边界距离
        float top = 25;//顶边距
        float bottom = 10;//底边距
        float side=0;//格子边长
        bool showGUI = false;
        bool dataLoadOver = false;
        int GUIstate = 0;//该显示哪个GUI界面
        int selectedItemId = 0;//选中的物品
        int yield = 0;//产量
        float offset = 0;//溢出偏移
        float offset_y = 0;//纵向溢出偏移
        int maxColumn = 0;//现有最大列号
        Texture2D background;
        //
        Material lineMaterial;
        //
        Hashtable customRecipes = new Hashtable();//修改过的配方
        Hashtable customFactory = new Hashtable();//修改过的工作台
        Hashtable defaultRecipes = new Hashtable();//默认配方
        int defaultFactory=-2;//默认工厂
        string lastCustomedTarget=null;//上一个修改过的物品的标记
        private void Start()
        {
            Harmony.CreateAndPatchAll(typeof(QTools), null);
            InitTranslateMap();


        }
        void OnGUI()
        {
            if (Input.GetKeyDown(KeyCode.BackQuote) && !keyLock)
            {
                keyLock = true;
                if (!showGUI)
                {
                    showGUI = true;

                    if (!lineMaterial)
                    {
                        lineMaterial = new Material(Shader.Find("Mobile/VertexLit"));
                    }
                    if (UIRoot.instance != null)
                    {
                        UIRoot.instance.OpenLoadingUI();
                    }
                }
                else
                {
                    showGUI = false;
                    if (UIRoot.instance != null)
                    {
                        UIRoot.instance.CloseLoadingUI();
                    }
                }
                
            }
            if (Input.GetKeyUp(KeyCode.BackQuote) && keyLock)
            {
                keyLock = false;
            }
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                GUIstate = 0;
                yield = 0;
                offset = 0;
                offset_y = 0;
                customRecipes.Clear();
                customFactory.Clear();
            }
            if (showGUI)
            {
                windowRect.width = Screen.width;
                windowRect.height = Screen.height;
                max -= (int)Input.mouseScrollDelta.y;
                max=max < 15 ? 15 : max;
                max = max > 52 ? 52 : max;
                side = (windowRect.width - 2 * leftAndRight - (max - 1) * line) / max;
                GUI.skin.label.fontSize = (int)(side / 5);
                GUI.skin.button.fontSize = (int)(side / 5);
                GUI.skin.textField.fontSize = (int)(side / 5);
                windowRect = GUI.Window(0, windowRect, drawWindow, "QTools");
            }
        }
        void drawWindow(int WindowID)
        {
            if (background == null)
            {
                background = new Texture2D(10, 10);
                GUI.skin.label.normal.textColor = Color.black;
            }
            if (GUIstate == 1)
            {
                GUI.DrawTexture(new Rect(0, 0, windowRect.width, windowRect.height), background);
            }
            if (GameMain.instance != null || dataLoadOver)
            {
                dataLoadOver = true;
                switch (GUIstate)
                {
                    case 0:
                        showItems();
                        break;
                    case 1:
                        calAndShowResult();
                        break;
                }
            }
            else
            {
                GUI.Label(new Rect(10, 20, windowRect.width, windowRect.height), translate("等待游戏资源加载!"));
            }
            
        }
        void showItems()
        {
            int i = 0;
            foreach(ItemProto tempItemProto in LDB.items.dataArray)
            {
                if (tempItemProto.recipes.Count > 0)
                {
                    if (GUI.Button(new Rect(leftAndRight+i%max*(side+line), top + i / max * (side + line), side, side), tempItemProto.iconSprite.texture))
                    {
                        selectedItemId = tempItemProto.ID;
                        GUIstate = 1;  
                    }
                    i++;
                }
            }
        }
        void calAndShowResult()
        {
            ArrayList rawLayerList = new ArrayList();
            Hashtable rawCounts = new Hashtable();
            Hashtable byProductsCounts = new Hashtable();
            Hashtable bySelfCounts = new Hashtable();
            Hashtable FactoryCounts = new Hashtable();
            float left = leftAndRight - offset;
            float topOfMap = top - offset_y;
            string tempYield = GUI.TextField(new Rect(left + (side + line) * 2, topOfMap, side, side), yield.ToString(), 6);
            if (tempYield.Length>="`".Length && tempYield.IndexOf("`") != -1 || tempYield.Length >= "·".Length && tempYield.IndexOf("·") != -1)
            {
                showGUI = false;
                if (UIRoot.instance != null)
                {
                    UIRoot.instance.CloseLoadingUI();
                }
            }
            else
            {
                int.TryParse(tempYield, out yield);
            }
            
            maxColumn = 0;
            calRaws(selectedItemId, yield, 0,"0",rawLayerList);
            lastCustomedTarget = null;//使LastCustomedTarget只起一次作用
            defaultFactory = -2;//使统一合成台的任务只执行一次
            defaultRecipes.Clear();//使统一配方的任务只执行一次
            optimizationResults(rawLayerList);//副产物优化,未实现
            if (Input.GetMouseButtonDown(0) && !mouseLock)
            {
                mouseLock = true;
                lastMousePosition = Input.mousePosition;
            }
            if(Input.GetMouseButtonUp(0) && mouseLock)
            {
                mouseLock = false;
            }
            if(Input.GetMouseButton(0) && mouseLock)
            {
                if(offset>=0 && offset <= getMaxOffset())
                {
                    offset += lastMousePosition.x - Input.mousePosition.x;
                }
                else
                {
                    offset = offset < 0 ? 0 : getMaxOffset();
                }
                lastMousePosition.x = Input.mousePosition.x;
                if (offset_y >= 0 && offset_y <= getMaxOffset_y(rawLayerList))
                {
                    offset_y -= lastMousePosition.y - Input.mousePosition.y;
                }
                else
                {
                    offset_y = offset_y < 0 ? 0 : getMaxOffset_y(rawLayerList);
                }
                lastMousePosition.y = Input.mousePosition.y;
            }
            for (int i = 0; i < rawLayerList.Count; i++)
            {
                ArrayList tempLayerList = (ArrayList)rawLayerList[i];
                for (int j = 0; j < tempLayerList.Count; j++)
                {
                    
                    Hashtable tempItemInfo = (Hashtable)tempLayerList[j];

                    int itemId= (int)tempItemInfo["itemId"];
                    double count = (double)tempItemInfo["count"];
                    int type = (int)tempItemInfo["type"];
                    string target=(string)tempItemInfo["target"];
                    int column = (int)tempItemInfo["column"];
                    string typeTarget;

                    switch (type)
                    {
                        case 0:
                            typeTarget = translate("原料")+"\n";
                            if (rawCounts.ContainsKey(itemId))
                            {
                                rawCounts[itemId] = (double)rawCounts[itemId] + count;
                            }
                            else
                            {
                                rawCounts[itemId] =  count;
                            }
                            break;
                        case 1:
                            typeTarget = translate("自提供")+"\n";
                            if (bySelfCounts.ContainsKey(itemId))
                            {
                                bySelfCounts[itemId] = (double)bySelfCounts[itemId] + count;
                            }
                            else
                            {
                                bySelfCounts[itemId] = count;
                            }
                            break;
                        case 2:
                            typeTarget = translate("副产物")+"\n";
                            if (byProductsCounts.ContainsKey(itemId))
                            {
                                byProductsCounts[itemId] = (double)byProductsCounts[itemId] + count;
                            }
                            else
                            {
                                byProductsCounts[itemId] = count;
                            }
                            break;
                        case 3:
                            typeTarget = i==0? translate("目标")+"\n":translate("临时物")+"\n";
                            break;
                        case 4:
                            typeTarget = translate("加工厂")+"\n";
                            if (FactoryCounts.ContainsKey(itemId))
                            {
                                FactoryCounts[itemId] = (double)FactoryCounts[itemId] + count;
                            }
                            else
                            {
                                FactoryCounts[itemId] = count;
                            }
                            break;
                        default:
                            typeTarget = translate("未知")+"\n";
                            break;
                    }
                    if (type==0 || type==2)
                    {
                        GUI.Box(new Rect(left + ((column) * (side + line)), topOfMap + i * 2 * (side + line), side, side), LDB.items.Select(itemId).iconSprite.texture); 
                    }
                    else if(type==1 || type==3)
                    {
                        if(GUI.Button(new Rect(left + (( column) * (side + line)), topOfMap + i * 2 * (side + line), side, side), LDB.items.Select(itemId).iconSprite.texture))
                        {
                            if (Input.GetMouseButtonUp(1))
                            {
                                defaultRecipes.Add(itemId,customRecipes[target]);
                            }
                            else
                            {
                                if ((int)customRecipes[target] + 1 >= LDB.items.Select(itemId).recipes.Count)
                                {
                                    customRecipes[target] = -1;
                                }
                                else
                                {
                                    int actualId = LDB.items.Select(itemId).recipes[(int)customRecipes[target] + 1].ID;
                                    if (actualId == 58 || actualId == 115)
                                    {
                                        if ((int)customRecipes[target] + 2 >= LDB.items.Select(itemId).recipes.Count)
                                        {
                                            customRecipes[target] = -1;
                                        }
                                        else
                                        {
                                            customRecipes[target] = (int)customRecipes[target] + 2;
                                        }
                                    }
                                    else
                                    {
                                        customRecipes[target] = (int)customRecipes[target] + 1;
                                    }

                                }
                                lastCustomedTarget = target;
                            }

                            
                        }
                    }
                    
                    else if (type == 4)
                    {
                        if (itemId >= 2303 && itemId <= 2305)
                        {
                            if (GUI.Button(new Rect(left + ((column) * (side + line)), topOfMap + i * 2 * (side + line), side, side), LDB.items.Select(itemId).iconSprite.texture))
                            {
                                if (Input.GetMouseButtonUp(1))
                                {
                                    defaultFactory = itemId;
                                }
                                else
                                {
                                    customFactory[target] = (itemId - 2303 + 1) % 3 + 2303;
                                }

                            }
                        }
                        else
                        {
                            GUI.Box(new Rect(left + ((column) * (side + line)), topOfMap + i * 2 * (side + line), side, side), LDB.items.Select(itemId).iconSprite.texture);
                        }
                    }
                    if(type==4 && (bool)tempItemInfo["notFull"])
                    {
                        GUI.skin.label.normal.textColor = Color.red;
                    }
                    GUI.Label(new Rect(left + ((column) * (side + line)), topOfMap + (i * 2 + 1) * (side + line), side, side), typeTarget + count+(type==4?"":"/min"));
                    GUI.skin.label.normal.textColor = Color.black;
                    if (i > 0 && type!=2)
                    {
                        DrawLine(left + ((column) * (side + line)) + side / 2, topOfMap + i * 2 * (side + line), left + (getParentColumn(target, i, rawLayerList) * (side + line)) + side / 2, topOfMap + (i - 1) * 2 * (side + line) + side);
                    }
                }
            }
            //画统计
            int itemTypeCount = 0;
            foreach (int itemId in rawCounts.Keys)
            {
                GUI.Box(new Rect(left + itemTypeCount * (side + line), topOfMap+(rawLayerList.Count*2+3)*(side+line),side,side), LDB.items.Select(itemId).iconSprite.texture);
                GUI.Label(new Rect(left + itemTypeCount * (side + line), topOfMap + (rawLayerList.Count * 2 + 4) * (side + line), side, side), translate("原料")+"\n" +rawCounts[itemId].ToString()+"/min");
                itemTypeCount++;
            }
            foreach (int itemId in bySelfCounts.Keys)
            {
                GUI.Box(new Rect(left + itemTypeCount * (side + line), topOfMap + (rawLayerList.Count * 2 + 3) * (side + line), side, side), LDB.items.Select(itemId).iconSprite.texture);
                GUI.Label(new Rect(left + itemTypeCount * (side + line), topOfMap + (rawLayerList.Count * 2 + 4) * (side + line), side, side), translate("自提供")+"\n" +bySelfCounts[itemId].ToString()+"/min");
                itemTypeCount++;
            }
            foreach (int itemId in FactoryCounts.Keys)
            {
                GUI.Box(new Rect(left + itemTypeCount * (side + line), topOfMap + (rawLayerList.Count * 2 + 3) * (side + line), side, side), LDB.items.Select(itemId).iconSprite.texture);
                GUI.Label(new Rect(left + itemTypeCount * (side + line), topOfMap + (rawLayerList.Count * 2 + 4) * (side + line), side, side), translate("加工厂")+"\n" + FactoryCounts[itemId].ToString());
                itemTypeCount++;
            }
            foreach (int itemId in byProductsCounts.Keys)
            {
                GUI.Box(new Rect(left + itemTypeCount * (side + line), topOfMap + (rawLayerList.Count * 2 + 3) * (side + line), side, side), LDB.items.Select(itemId).iconSprite.texture);
                GUI.Label(new Rect(left + itemTypeCount * (side + line), topOfMap + (rawLayerList.Count * 2 + 4) * (side + line), side, side), translate("副产物")+"\n" +byProductsCounts[itemId].ToString()+"/min");
                itemTypeCount++;
            }


        }
        void calRaws(int itemId,double count,int layer, string target,ArrayList rawLayerList)
        {
            bool hasByProducts = false;
            if (LDB.items.Select(itemId).recipes.Count > 0)//有合成配方的物品
            {
                int customedRecipeId;
                //如果没有设置自定义配方或本物品已经设置的自定义配方属于上一个修改自定义配方的子树，则本物品自定义配方恢复为默认配方
                if (defaultRecipes.ContainsKey(itemId))
                {
                    customRecipes[target] = defaultRecipes[itemId];
                }
                else if (!customRecipes.ContainsKey(target) || lastCustomedTarget!=null && lastCustomedTarget.Length < target.Length && target.IndexOf(lastCustomedTarget,0,lastCustomedTarget.Length)!=-1)
                {
                    customRecipes[target] = 0;
                }
                customedRecipeId = (int)customRecipes[target];
                //自定义配方id为-1时表示自提供该物品
                if (customedRecipeId == -1)
                {
                    if (rawLayerList.Count < layer + 1)
                    {
                        rawLayerList.Add(new ArrayList());
                    }
                    Hashtable itemInfo = new Hashtable();
                    itemInfo.Add("itemId", itemId);
                    itemInfo.Add("count", count);
                    itemInfo.Add("type", 1);
                    itemInfo.Add("target", target);
                    itemInfo.Add("column", maxColumn);
                    ((ArrayList)rawLayerList[layer]).Add(itemInfo);
                    return;
                }
                //获取该物品的指定配方
                RecipeProto recipe = LDB.items.Select(itemId).recipes[customedRecipeId];
                //计算该物品对于配方的倍率
                double times = count;
                for(int i = 0; i < recipe.Results.Length; i++)//计算配方倍率
                {
                    if (recipe.Results[i]==itemId)
                    {
                        times = count/recipe.ResultCounts[i];
                        break;
                    }
                }
                //本物品层
                if (rawLayerList.Count < layer + 1)
                {
                    rawLayerList.Add(new ArrayList()); 
                }
                
                //加本层合成物
                for (int i=0;i<recipe.Results.Length;i++)
                {
                    bool isByProducts = recipe.Results[i] != itemId;
                    if (isByProducts)
                    {
                        hasByProducts = true;
                    }
                    else
                    {
                        //加工厂信息
                        if (rawLayerList.Count < layer + 2)
                        {
                            rawLayerList.Add(new ArrayList());
                        }
                        int factId = 0;
                        double time_times = 1;
                        switch (recipe.Type)
                        {
                            case ERecipeType.None:
                                break;
                            case ERecipeType.Smelt:
                                factId = 2302;
                                break;
                            case ERecipeType.Chemical:
                                factId = 2309;
                                break;
                            case ERecipeType.Refine:
                                factId = 2308;
                                break;
                            case ERecipeType.Assemble:
                                //三种制造台
                                if (defaultFactory != -2)
                                {
                                    customFactory[target] = defaultFactory;
                                }
                                else if (!customFactory.ContainsKey(target) || lastCustomedTarget != null && lastCustomedTarget.Length <= target.Length && target.IndexOf(lastCustomedTarget, 0, lastCustomedTarget.Length) != -1)
                                {
                                    customFactory[target] = 2304;
                                }
                                
                                factId = (int)customFactory[target];
                                if (factId == 2303)
                                {
                                    time_times = 0.75;
                                }else if (factId == 2304)
                                {
                                    time_times = 1;
                                }
                                else
                                {
                                    time_times = 1.5;
                                }
                                break;
                            case ERecipeType.Particle:
                                factId = 2310;
                                break;
                            case ERecipeType.Exchange:
                                factId = 2209;
                                break;
                            case ERecipeType.PhotonStore:
                                factId = 2208;
                                break;
                            case ERecipeType.Fractionate:
                                factId = 2314;
                                break;
                            case ERecipeType.Research:
                                factId = 2901;
                                break;
                        }
                        double factCount = times / (time_times * (60.0 / (recipe.TimeSpend / 60.0)));
                        bool notFull = false;
                        if ((int)factCount < factCount)
                        {
                            notFull = true;
                            factCount = (int)factCount + 1;
                        }
                        Hashtable factoryInfo = new Hashtable();
                        factoryInfo.Add("itemId", factId);
                        factoryInfo.Add("count", factCount);
                        factoryInfo.Add("type", 4);
                        factoryInfo.Add("target", target);
                        factoryInfo.Add("column", maxColumn);
                        factoryInfo.Add("notFull", notFull);
                        ((ArrayList)rawLayerList[layer+1]).Add(factoryInfo);
                    }
                    Hashtable itemInfo = new Hashtable();
                    itemInfo.Add("itemId", recipe.Results[i]);
                    itemInfo.Add("count", recipe.ResultCounts[i] * times);
                    itemInfo.Add("type", isByProducts?2:3);
                    itemInfo.Add("target", target);
                    itemInfo.Add("column", maxColumn+(isByProducts?1:0));
                    ((ArrayList)rawLayerList[layer]).Add(itemInfo);
                }
                //计算本层所需原料
                for (int i= 0;i < recipe.Items.Length;i++)
                {
                    if (i > 0)
                    {
                        maxColumn++;
                    }
                    calRaws(recipe.Items[i], recipe.ItemCounts[i]*times, layer + 2, target+i,rawLayerList);
                }
                if (hasByProducts)
                {
                    maxColumn++;
                }
            }
            else
            {
                //加本层原料
                if (rawLayerList.Count < layer + 1)
                {
                    rawLayerList.Add(new ArrayList());
                }
                Hashtable itemInfo = new Hashtable();
                itemInfo.Add("itemId", itemId);
                itemInfo.Add("count", count);
                itemInfo.Add("type", 0);
                itemInfo.Add("target", target);
                itemInfo.Add("column", maxColumn);
                ((ArrayList)rawLayerList[layer]).Add(itemInfo);
            }
        }
        void optimizationResults(ArrayList rawLayerList)
        {
            //为方便生产线边造边用，只允许前方生产线的副产品运送到后方
            //不会写，再见
            //以后再优化
        }
        void DrawLine(float x1, float y1, float x2, float y2)
        {
            
            x1 = windowRect.x + x1;
            x2 = windowRect.x + x2;
            y1 = windowRect.y + y1;
            y2 = windowRect.y + y2;
            y1 = windowRect.height - y1;
            y2 = windowRect.height - y2;
            GL.PushMatrix(); //保存当前Matirx
            lineMaterial.SetPass(0); //刷新当前材质
            GL.LoadPixelMatrix();//设置pixelMatrix
            //GL.Color(Color.yellow);
            GL.Begin(GL.LINES);
            GL.Vertex3(x1, y1, 0);
            GL.Vertex3(x1, (y1 + y2) / 2, 0);
            GL.Vertex3(x1, (y1 + y2) / 2, 0);
            GL.Vertex3(x2, (y1 + y2) / 2, 0);
            GL.Vertex3(x2, (y1 + y2) / 2, 0);
            GL.Vertex3(x2, y2, 0);

            GL.End();
            GL.PopMatrix();//读取之前的Matrix
        }
        float getMaxOffset()
        {
            if (maxColumn+5 < max)
            {
                return 0;
            }
            else
            {
                return (maxColumn - max+5) * (side + line);
            }
        }
        float getMaxOffset_y(ArrayList rawLayerList)
        {
            if (rawLayerList.Count*2 + 5 < (windowRect.height-top-bottom)/(side+line))
            {
                return 0;
            }
            else
            {
                return (rawLayerList.Count*2 - (windowRect.height - top - bottom) / (side + line) + 5) * (side + line);
            }
        }
        int getParentColumn(string target,int row,ArrayList rawLayerList)
        {
            ArrayList tempLayerList = (ArrayList)rawLayerList[row-1];
            foreach (Hashtable tempItemInfo in tempLayerList)
            {
                int type = (int)tempItemInfo["type"];
                string parentTarget = (string)tempItemInfo["target"];
                int column = (int)tempItemInfo["column"];
                if(target.IndexOf(parentTarget,0,parentTarget.Length)!=-1 && type != 2)
                {
                    return column;
                }
            }
            return 0;
        }
        void InitTranslateMap()
        {
            translateMap.Clear();
            translateMap.Add("等待游戏资源加载!", "Wait for game resources to load!");
            translateMap.Add("关闭", "Close");
            translateMap.Add("返回", "Return");
            translateMap.Add("原料", "raw");
            translateMap.Add("自提供", "bySelf");
            translateMap.Add("副产物", "byProducts");
            translateMap.Add("目标", "target");
            translateMap.Add("临时物", "temp");
            translateMap.Add("加工厂", "factory");
            translateMap.Add("未知", "unknown");
        }
        string translate(string text)
        {
            if (translateMap.ContainsKey(text) && Localization.language != Language.zhCN)
            {
                return translateMap[text].ToString();
            }
            else
            {
                return text;
            }
        }
    }
}