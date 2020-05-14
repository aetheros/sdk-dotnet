import { Grid } from "@material-ui/core";
import { createStyles, Theme, ThemeProvider, WithStyles, withStyles } from '@material-ui/core/styles';
import dotnetify, { dotnetifyVM } from "dotnetify";
import * as React from "react";
import auth from "../auth";
import BasePage from "../components/BasePage";
import CommandDialog from "../components/meter/CommandDialog";
import DataSummation from "../components/meter/DataSummation";
import { DataSummaryPoint } from "../components/meter/DataSummationChart";
import { MeterCommand, MeterCommandModel } from "../components/meter/MeterCommand";
import { MeterConfig, MeterReadPolicy } from "../components/meter/MeterConfig";
import MeterDetail from "../components/meter/MeterDetail";
import { MeterEvent, MeterEvents } from "../components/meter/MeterEvents";
import PolicyConfigDialog from "../components/meter/PolicyConfigDialog";
import defaultTheme from '../styles/theme-default';

const styles = (theme: Theme) => createStyles({
	addButton: { margin: "1em" },
	grow: {
		flexGrow: 1,
	},
});

interface Props extends WithStyles<typeof styles> {
};

type Summation = {
	readTime: string;
	value: number;
};
type Data = {
	Summations: Summation[];
};
type Info = {
	meterId: string;
};
type SavedSendCommand = {
	MeterId: string;
	Action: string;
	When: string;
};
type Events = {
	MeterEvents: MeterEvent[];
};

type State = {
	//MeterEvents: Events;
	//ActionType: any;
	//LatestDataValue: any;
	Summations: Data;
	//Info: Info;
	MeterState: string;
	MeterReadPolicy: MeterReadPolicy;
	MeterCommand: MeterCommandModel;
	SummationWindow: number;
	//OldData: Array<any>;
	//OldEvents: Array<any>;
	ConfigDialogOpen: boolean;
	CommandDialogOpen: boolean;
	MeterId: string;
	OpenValve: string;
	CommandWhen: string;
	ConfigPolicyName: string;
	ConfigPolicyStart: string;
	ConfigPolicyEnd: string;
	ConfigPolicyInterval: any;
};

class MeterDashboard extends React.Component<Props, State> {
	vm: dotnetifyVM;
	dispatch: (state: any) => any;

	chartData: DataSummaryPoint[] = [];
	events: MeterEvent[] = [];

	chartOptions = {
		title: {
			display: true,
		},
		scales: {
			xAxes: [{
				type: 'realtime',
				realtime: {
					duration: 30 * 60 * 1000,
					delay: 5000,
				}

			}],
			yAxes: [{
				scaleLabel: {
					display: true,
					labelString: 'value'
				}
			}]
		},
		tooltips: {
			mode: 'nearest',
			intersect: false
		},
		hover: {
			mode: 'nearest',
			intersect: false
		}
	};

	constructor(props: Props) {
		super(props);
		this.vm = dotnetify.react.connect("MeterDashboard", this, {
			exceptionHandler: ex => {
				alert(ex.message);
				auth.signOut();
			}
		});

		this.dispatch = state => this.vm.$dispatch(state);

		this.state = {
			//MeterEvents: null,
			//ActionType: null,
			//LatestDataValue: 0,
			//Info: null,
			SummationWindow: 1 * 24 * 60,
			//OldData: [],
			//OldEvents: [],
			ConfigDialogOpen: false,
			CommandDialogOpen: false,
			MeterState: null,
			MeterCommand: null,
			OpenValve: null,
			CommandWhen: null,
		} as State;
	}

	shouldComponentUpdate(nextProps, nextState) {
		const oldData = nextState.OldData;
		if (oldData) {
			this.chartData = oldData.map(d => { return { x: new Date(d.readTime).toLocaleString(), y: d.value } });
		}

		const currentData = this.state.Summations;
		if (currentData) {
			currentData.Summations.map(d => {
				var foundCurrentData = this.chartData.find(function (e) {
					return e.x === new Date(d.readTime).toLocaleString();
				});
				if (!foundCurrentData) {
					this.chartData.push({ x: d.readTime, y: d.value });
				}
			})
		}

		const data = nextState.Summations;
		if (data) {
			console.log(data);

			data.Summations.map(d => {
				var foundData = this.chartData.find(function (e) {
					return e.x === new Date(d.readTime).toLocaleString();
				});
				if (!foundData) {
					this.chartData.push({ x: d.readTime, y: d.value });
				}
			})
		}

		const oldEvents = nextState.OldEvents;
		if (oldEvents) {
			this.events = oldEvents;
		}
		const newEvent = nextState.MeterEvents;
		if (newEvent) {
			newEvent.MeterEvents.map(m => {
				var foundEvent = this.events.find(function (e) {
					return e.EventTime === m.EventTime;
				});
				if (!foundEvent) {
					this.events.unshift(m);
				}
			});
		}

		return true;
	}

	componentWillUnmount() {
		this.vm.$destroy();
	}

	handleChangeWindow(event, idx, value) {
		this.setState({
			SummationWindow: value,
			//Summations: []
		});

		this.dispatch({ UpdateSummationWindow: value });
	};

	handleClickConfigOpen() {
		this.setState({ ConfigDialogOpen: true });
	};

	handleConfigClose() {
		this.setState({ ConfigDialogOpen: false });
	};

	handleClickCommandOpen() {
		this.setState({ CommandDialogOpen: true });
	};

	handleCommandClose() {
		this.setState({ CommandDialogOpen: false });
	};

	handleCommandSave() {
		this.dispatch({ SendCommand: { MeterId: this.state.MeterId, Action: this.state.OpenValve, When: this.state.CommandWhen } });

		this.setState({
			CommandDialogOpen: false,
		});
	};

	handleCommandValveChange(e) {
		this.setState({ OpenValve: e });
	};

	handleCommandWhenChange(e) {
		this.setState({ CommandWhen: e.toISOString() });
	};

	handleConfigNameChange(e) {
		this.setState({ ConfigPolicyName: e });
	}

	handleConfigStartChange(e) {
		this.setState({ ConfigPolicyStart: e.toISOString() });
	}

	handleConfigEndChange(e) {
		this.setState({ ConfigPolicyEnd: e.toISOString() });
	}

	handleConfigPolicyIntervalChange(e) {
		this.setState({ ConfigPolicyInterval: e });
	}

	handleConfigSave() {
		this.dispatch({
			SendMeterReadPolicy: {
				MeterId: this.state.MeterId,
				Name: this.state.ConfigPolicyName,
				Start: this.state.ConfigPolicyStart,
				End: this.state.ConfigPolicyEnd,
				ReadInterval: this.state.ConfigPolicyInterval
			}
		});

		this.setState({ ConfigDialogOpen: false });
	}

	render() {

		const dispatchState = state => {
			this.setState(state);
			this.vm.$dispatch(state);
		};

		return (
			<ThemeProvider theme={defaultTheme}>

				<BasePage title="Meter Dashboard" navigation="">
					<div className={this.props.classes.grow}>
						<Grid container spacing={1}>
							<Grid item xs={6}>
								<MeterDetail meterState={this.state.MeterState} meterId={this.state.MeterId} />
							</Grid>
							<Grid item xs={6}>
								<MeterCommand
									meterCommand={this.state.MeterCommand}
									handleCommandClick={this.handleClickCommandOpen.bind(this)} />
							</Grid>

							<Grid item xs={6}>
								<MeterConfig
									meterReadPolicy={this.state.MeterReadPolicy}
									handlePolicyConfigClick={this.handleClickConfigOpen.bind(this)} />
							</Grid>
							<Grid item xs={6}>
								<MeterEvents data={this.events} vm={this.vm} />
							</Grid>

							<Grid item xs={12}>
							</Grid>
						</Grid>
					</div>

					<DataSummation data={this.chartData} summationWindow={this.state.SummationWindow} meterId={this.state.MeterId} onWindowChange={this.handleChangeWindow.bind(this)} />

					<PolicyConfigDialog
						open={this.state.ConfigDialogOpen}
						onClose={this.handleConfigClose.bind(this)}
						onSave={this.handleConfigSave.bind(this)}
						onNameChange={this.handleConfigNameChange.bind(this)}
						onStartChange={this.handleConfigStartChange.bind(this)}
						onEndChange={this.handleConfigEndChange.bind(this)}
						onPolicyIntervalChange={this.handleConfigPolicyIntervalChange.bind(this)}
						meterReadPolicy={this.state.MeterReadPolicy}
						policyStart={this.state.ConfigPolicyStart}
						policyEnd={this.state.ConfigPolicyEnd}
					/>
					<CommandDialog
						open={this.state.CommandDialogOpen}
						onClose={this.handleCommandClose.bind(this)}
						onSave={this.handleCommandSave.bind(this)}
						valveState={this.state.MeterState}
						openValve={this.state.OpenValve}
						commandWhen={this.state.CommandWhen}
						onValveChange={this.handleCommandValveChange.bind(this)}
						onWhenChange={this.handleCommandWhenChange.bind(this)} />
				</BasePage>
			</ThemeProvider>
		);
	}
}

export default withStyles(styles)(MeterDashboard);
