import { Grid, Paper } from "@material-ui/core";
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
	gridRowItem: {
		height: '100%',
	}
});

interface Props extends WithStyles<typeof styles> {
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
enum Units {
	USGal = 1,
};
type Summation = {
	readTime: string;
	value: number;
};
type Data = {
	meterId: string;
	uom: Units;
	summations: Summation[];
};

type State = {
	MeterEvents: Events;
	//ActionType: any;
	//LatestDataValue: any;
	Summations: Data;
	//Info: Info;
	MeterState: string;
	MeterReadPolicy: MeterReadPolicy;
	MeterCommand: MeterCommandModel;
	SummationWindow: number;
	OldData: Summation[];
	OldEvents: MeterEvent[];
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

		this.state = {
			SummationWindow: 1 * 24 * 60,
			OldData: [],
			OldEvents: [],

			ConfigDialogOpen: false,
			CommandDialogOpen: false,
		} as State;
	}

	dispatch(state) {
		this.vm && this.vm.$dispatch(state);
	}

	componentWillUnmount() {
		this.vm && this.vm.$destroy();
	}

	shouldComponentUpdate(nextProps: Props, nextState: State, nextContent: any): boolean {

		const addSummations = (rg: Summation[]) => {
			for (let s of rg) {
				if (!this.chartData.some(old => old.key === s.readTime))
					this.chartData.push({ x: s.readTime, y: s.value, key: s.readTime });
			}
		}

		if (nextState.OldData) {
			for (let s of nextState.OldData)
				console.log('nextState.OldData : ' + s.readTime);
			addSummations(nextState.OldData);
		}

		if (this.state.Summations) {
			for (let s of this.state.Summations.summations)
				console.log('this.state : ' + s.readTime);
			addSummations(this.state.Summations.summations);
		}
		if (nextState.Summations) {
			for (let s of nextState.Summations.summations)
				console.log('nextState.Summations : ' + s.readTime);
			addSummations(nextState.Summations.summations);
		}


		for (let s of this.chartData)
			console.log('this.chartData : ' + s.x);

		console.log(this.chartData.length);

		const oldEvents = nextState.OldEvents;
		if (oldEvents) {
			this.events = oldEvents;
		}
		const newEvent = nextState.MeterEvents;
		if (newEvent) {
			newEvent.MeterEvents.map(m => {
				if (!this.events.some(function (e) {
					return e.EventTime === m.EventTime;
				})) {
					this.events.unshift(m);
				}
			});
		}

		return true;
	}

	handleChangeWindow(value) {
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
		const classes = this.props.classes;

		return (
			<ThemeProvider theme={defaultTheme}>

				<BasePage title="Meter Dashboard" navigation="">
					<Grid container className={classes.grow} direction="column" spacing={1}>
						<Grid item container className={classes.grow} direction="row" alignItems="stretch" spacing={1}>
							<Grid item xs>
								<Paper className={classes.gridRowItem}>
									<MeterDetail meterState={this.state.MeterState} meterId={this.state.MeterId} />
								</Paper>
							</Grid>
							<Grid item xs>
								<Paper className={classes.gridRowItem}>
									<MeterCommand
										meterCommand={this.state.MeterCommand}
										handleCommandClick={this.handleClickCommandOpen.bind(this)} />
								</Paper>
							</Grid>
						</Grid>

						<Grid item container className={classes.grow} direction="row" alignItems="stretch" spacing={1}>
							<Grid item xs>
								<Paper className={classes.gridRowItem}>
									<MeterConfig
										meterReadPolicy={this.state.MeterReadPolicy}
										handlePolicyConfigClick={this.handleClickConfigOpen.bind(this)} />
								</Paper>
							</Grid>
							<Grid item xs>
								<Paper className={classes.gridRowItem}>
									<MeterEvents data={this.events} vm={this.vm} />
								</Paper>
							</Grid>
						</Grid>

						<Grid item container className={classes.grow} direction="row" alignItems="stretch" spacing={1}>
							<Grid item xs>
								<Paper className={classes.gridRowItem}>
									<DataSummation
										data={this.chartData}
										summationWindow={this.state.SummationWindow}
										meterId={this.state.MeterId}
										onWindowChange={this.handleChangeWindow.bind(this)}
										onAddData={() => this.dispatch({ AddData: true })}
									/>
								</Paper>
							</Grid>
						</Grid>

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

					</Grid>
				</BasePage>
			</ThemeProvider>
		);
	}
}

export default Object.assign(withStyles(styles)(MeterDashboard), { name: '' });
