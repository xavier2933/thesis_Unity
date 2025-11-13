import pickle
with open('Demonstrations/PandaDemo.demo', 'rb') as f:
    data = pickle.load(f)
    print(f"Demo has {len(data)} episodes")
    print(f"Total steps: {sum(len(ep) for ep in data)}")